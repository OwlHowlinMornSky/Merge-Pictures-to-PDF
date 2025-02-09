using System.Runtime.InteropServices;

namespace PicMerge {
	/// <summary>
	/// 介于 图形界面 和 合成器 之间的 处理器。
	/// </summary>
	/// <param name="setBarNum">设置进度条进度的回调</param>
	/// <param name="setBarFinish">设置进度条完成的回调</param>
	/// <param name="warningBox">用于界面弹窗警告的回调</param>
	public partial class Processor(
		Action<int, int> setBarNum,
		Action setBarFinish,
		Action<string> warningBox
	) {
		private struct TaskInputData(TaskInputData.Type _type, List<string> _files) {
			public enum Type : byte {
				None = 0,
				Directory,
				FileNotArchive,
				Archive,
			}
			public readonly Type type = _type;
			public List<string> files = _files;
		}

		private class Count(int _v) {
			public int value = _v;
		}

		private readonly struct SaveParameters(PageParam _pp, ImageParam _ip) {
			public readonly PageParam pp = _pp;
			public readonly ImageParam ip = _ip;
		}
		private SaveParameters m_internalParam;
		private IOParam m_param;

		/// <summary>
		/// 修改进度条进度 的 回调。
		/// </summary>
		private readonly Action<int, int> SetBarNum = setBarNum;
		/// <summary>
		/// 修改进度条为任务完成 的 回调。
		/// </summary>
		private readonly Action SetBarFinish = setBarFinish;
		/// <summary>
		/// 弹出警告弹窗。
		/// </summary>
		private readonly Action<string> PopBoxWarning = warningBox;

		/// <summary>
		/// 防止多次start的锁。
		/// </summary>
		private readonly Count m_lockForRunning = new(0);

		/// <summary>
		/// 图片总计数。
		/// </summary>
		private int m_totalImgCnt = 0;
		/// <summary>
		/// 报告完成的图片计数。
		/// </summary>
		private readonly Count m_finishedImg = new(0);

		/// <summary>
		/// 任务过程中是否处理到报错。
		/// </summary>
		private bool m_haveFailedFiles = false;

		/// <summary>
		/// 用于子任务报告完成一张图片 的 回调目标。
		/// </summary>
		private void CallbackFinishOneImgFile() {
			lock (m_finishedImg) {
				m_finishedImg.value++;
				SetBarNum(m_finishedImg.value, m_totalImgCnt); // 更新进度条。
			}
		}

		/// <summary>
		/// 用于子任务报告全部完成 的 回调目标。实际上在本对象内的 ProcessSingleFiles 使用了，不需要传给PicMerge。
		/// </summary>
		private void CallbackFinishAllImgFile() {
			/// 没有等待处理的数据了。
			lock (m_finishedImg) {
				/// 图片也处理完了。
				if (m_finishedImg.value >= m_totalImgCnt) {
					SetBarFinish(); // 设置进度条为完成。
				}
			}
		}

		/// <summary>
		/// 本对象是否正在进行处理。
		/// </summary>
		/// <returns>是否正在进行</returns>
		public bool IsRunning() {
			lock (m_lockForRunning) {
				return m_lockForRunning.value != 0;
			}
		}

		/// <summary>
		/// 开始处理。
		/// </summary>
		/// <param name="paths">要处理的文件/文件夹</param>
		/// <returns>是否成功开始</returns>
		public async Task<bool> StartAsync(string[] paths, PicMerge.PageParam pp, PicMerge.ImageParam ip, IOParam param) {
			lock (m_lockForRunning) {
				if (m_lockForRunning.value != 0) {
					return false;
				}
				m_lockForRunning.value = 1;
			}
			if (!param.stayNoMove && string.IsNullOrEmpty(param.destinationPath)) {
				return false;
			}
			m_param = param;
			m_internalParam = new(pp, ip);
			Logger.Init();
			await Task.Run(() => { Process(paths); });
			Logger.Reset();
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
			lock (m_lockForRunning) {
				m_lockForRunning.value = 0;
			}
			return true;
		}

		/// <summary>
		/// 主任务过程。主任务预处理输入的路径，初步统计，并发配给子任务。
		/// </summary>
		/// <param name="paths">要处理的文件/文件夹</param>
		private void Process(string[] paths) {
			Queue<TaskInputData> waitings = [];
			{
				/// 拖放进入的文件列表 通常 只 包括 文件与文件夹。零散文件 即 直接被拖入的文件。
				/// 一般来说，零散文件都在同一个目录，我也懒得考虑不同目录了。
				/// 零散文件的列表。
				List<string> files = [];
				/// 文件夹列表，第一个是基准文件夹的绝对路径，第二个是相对路径。合起来才表示处理路径。
				List<Tuple<string, string>> directories = [];
				/// 无法处理的列表（不是文件也不是文件夹）。
				List<string> unknown = [];

				{
					/// 初步扫描统计。
					m_totalImgCnt = ScanInput(paths, ref files, ref directories, ref unknown);

					/// 设置统计值。
					m_finishedImg.value = 0;
					if (m_totalImgCnt < 1)
						return;

					SetBarNum(0, m_totalImgCnt);
				}

				/// 准备子任务。
				/// 报告无法处理的列表。
				m_haveFailedFiles = false;
				if (unknown.Count > 0) {
					foreach (string str in unknown) {
						Logger.Log($"[Cannot Process] \"{str}\".");
					}
					m_haveFailedFiles = true;
				}
				/// 文件夹。
				if (directories.Count > 0) {
					foreach (Tuple<string, string> pair in directories)
						waitings.Enqueue(new TaskInputData(TaskInputData.Type.Directory, [pair.Item1, pair.Item2]));
				}
				/// 零散文件。
				if (files.Count > 0) {
					int archiveCnt = -1;
					if (directories.Count < 1) { // 没有拖入目录（即只有文件）
						archiveCnt = files.Where(x => {
							string ext = Path.GetExtension(x);
							bool isZip = ext.Equals(".zip", StringComparison.OrdinalIgnoreCase);
							bool is7z = ext.Equals(".7z", StringComparison.OrdinalIgnoreCase);
							bool isRar = ext.Equals(".rar", StringComparison.OrdinalIgnoreCase);
							return isZip || is7z || isRar;
						}).Count();
					}
					if (archiveCnt == files.Count) { // 只有压缩文件
						waitings.Enqueue(new TaskInputData(TaskInputData.Type.Archive, files));
					}
					else {
						waitings.Enqueue(new TaskInputData(TaskInputData.Type.FileNotArchive, files));
					}
				}
			}
			/// 正常情况下的处理。不考虑压缩文件。
			while (waitings.Count > 0) {
				ProcessOneItem(waitings.Dequeue());
			}
			if (m_haveFailedFiles) {
				PopBoxWarning(Logger.FilePath);
			}
		}

		private void ProcessOneItem(TaskInputData data) {
			switch (data.type) {
			case TaskInputData.Type.Directory: {
				string baseDir = data.files[0];
				string relative = data.files[1];

				bool relativeIsEmpty = string.IsNullOrEmpty(relative);

				string srcDir = Path.Combine(baseDir, relative);
				string dstDir;

				if (m_param.stayNoMove) {
					dstDir = Path.GetDirectoryName(srcDir) ?? srcDir;
				}
				else if (m_param.keepStruct) {
					dstDir = Path.Combine(m_param.destinationPath, relative);
					if (!m_param.keepPdfInFolder && !relativeIsEmpty)
						dstDir = Path.GetDirectoryName(dstDir) ?? dstDir;
				}
				else {
					dstDir = m_param.destinationPath;
				}

				string outputPath = EnumFileName(dstDir, m_param.keepPdfInFolder ? "Images" : Path.GetFileName(srcDir), ".pdf");

				/// 文件夹对应标题 取 它与基准路径相差的相对路径。
				ProcessOneFolder(srcDir, outputPath, relativeIsEmpty ? Path.GetFileName(baseDir) : relative);
				break;
			}
			case TaskInputData.Type.FileNotArchive: {
				string pathOfFirstFile = data.files[0];
				string dirOfFirstFile = Path.GetDirectoryName(pathOfFirstFile) ?? "";

				/// 输出到原位时，就是输入路径的父目录，否则，就是选定的目标目录。
				string outputPath = EnumFileName(
					m_param.stayNoMove ? dirOfFirstFile : m_param.destinationPath,
					"Images", ".pdf"
				);

				/// 零散文件的标题 取 文件所在父目录的名字。
				ProcessFiles(data.files, outputPath, Path.GetFileName(dirOfFirstFile));
				break;
			}
			case TaskInputData.Type.Archive: {
				ProcessArchive(data.files);
				break;
			}
			default:
				CallbackFinishAllImgFile();
				break;
			}
		}

		/// <summary>
		/// 子任务过程：处理文件夹。
		/// </summary>
		/// <param name="sourceDir">输入目录</param>
		/// <param name="outputPath">输出文件</param>
		/// <param name="title">内定标题（不是文件名）</param>
		private void ProcessOneFolder(string sourceDir, string outputPath, string? title) {
			List<string> files = Directory.EnumerateFiles(sourceDir).ToList();
			ProcessFiles(files, outputPath, title);
		}

		/// <summary>
		/// 子任务过程：处理零散文件。
		/// </summary>
		/// <param name="files">文件列表</param>
		/// <param name="outputPath">输出文件</param>
		/// <param name="title">内定标题（不是文件名）</param>
		private void ProcessFiles(List<string> files, string outputPath, string? title) {
			/// 按字符串逻辑排序。资源管理器就是这个顺序，可以使 2.png 排在 10.png 前面，保证图片顺序正确。
			files.Sort(StrCmpLogicalW);
			IMerger merger = IMerger.Create(
				true,
				CallbackFinishOneImgFile,
				m_internalParam.pp,
				m_internalParam.ip
			);
			List<IMerger.FileResult> result = merger.Process(outputPath, files, title);
			CallbackFinishAllImgFile();
			CheckResultListFailed(title ?? outputPath, ref result);
		}

		private void ProcessArchive(List<string> files) {
			files.Sort(StrCmpLogicalW);
			IMerger merger = IMerger.CreateArchiveConverter(
				CallbackFinishOneImgFile,
				m_param.stayNoMove,
				m_param.keepStruct,
				m_internalParam.pp,
				m_internalParam.ip
			);
			List<IMerger.FileResult> failed = merger.Process(m_param.destinationPath, files);
			CallbackFinishAllImgFile();
			CheckResultListFailed(m_param.destinationPath, ref failed);
		}

		/// <summary>
		/// 初步扫描拖入路径。
		/// </summary>
		/// <param name="paths">拖入的路径</param>
		/// <param name="files">输出零散文件</param>
		/// <param name="directories">输出文件夹</param>
		/// <param name="unknown">输出未知内容</param>
		/// <returns></returns>
		private int ScanInput(
			string[] paths,
			ref List<string> files,
			ref List<Tuple<string, string>> directories,
			ref List<string> unknown
		) {
			int cnt = 0;
			List<string> tmpDirs = [];
			foreach (var path in paths) {  // 遍历拖入的路径。
				if (File.Exists(path)) {   // 是否是文件。
					files.Add(path);
					cnt++;
				}
				else if (Directory.Exists(path)) { // 是否是文件夹。
					tmpDirs.Add(path);
				}
				else {
					unknown.Add(path); // 加入无法处理的列表。
					cnt++;
				}
			}
			/// 按字符串逻辑排序。资源管理器就是这个顺序，可以使 2.png 排在 10.png 前面，保证图片顺序正确。
			tmpDirs.Sort(StrCmpLogicalW);
			foreach (var path in tmpDirs) {
				int dirfilecnt = Directory.EnumerateFiles(path).Count();
				var dirParent = Path.GetDirectoryName(path);
				if (dirParent == null) {
					if (dirfilecnt > 0) { // 不为空 才 加入。
						directories.Add(Tuple.Create(path, ""));
						cnt += dirfilecnt;
					}
					if (m_param.recursion)
						cnt += RecursionAllDirectories(path, path, ref directories);
				}
				else {
					if (dirfilecnt > 0) { // 不为空 才 加入。
						directories.Add(Tuple.Create(dirParent, Path.GetRelativePath(dirParent, path)));
						cnt += dirfilecnt;
					}
					if (m_param.recursion)
						cnt += RecursionAllDirectories(path, dirParent, ref directories);
				}
			}
			return cnt;
		}

		/// <summary>
		/// 子任务步骤：弹窗报告失败的文件。
		/// </summary>
		/// <param name="title">内定标题</param>
		/// <param name="result">结果列表</param>
		private void CheckResultListFailed(string title, ref List<IMerger.FileResult> result) {
			var failed = result.Where(r => r.code > 0x80000000);
			if (failed.Any()) {
				foreach (var str in failed) {
					Logger.Log($"[Cannot Merge] At \"{title}\" from \"{str.filename}\", because \"{str.description}\" (Code: {str.code:X}).");
				}
				m_haveFailedFiles = true;
			}
		}

		/// <summary>
		/// 枚举可用的文件名。防止输出文件名与现有文件相同导致覆盖。
		/// 但是扫描可用文件名与开始写入有一定时间差，
		/// 如果用户~脑残到~在这段时间创建同名文件，可能会出问题。
		/// </summary>
		/// <param name="dir">文件将所处的目录</param>
		/// <param name="stem">文件的期望名称</param>
		/// <param name="exname">文件的扩展名</param>
		/// <returns>添加可能的" (%d)"后，不与现有文件同名的文件路径</returns>
		private static string EnumFileName(string dir, string stem, string exname) {
			string res = Path.Combine(dir, stem + exname);
			int i = 0;
			while (File.Exists(res)) {
				i++;
				res = Path.Combine(dir, $"{stem} ({i}){exname}");
			}
			return res;
		}

		/// <summary>
		/// 递归扫描文件夹。会将子目录加入list（即不加入dir），返回包括根的子树（即包括dir所含文件）的文件数统计。
		/// </summary>
		/// <param name="dir">当前目录</param>
		/// <param name="basedir">基准目录</param>
		/// <param name="list">输出到的列表</param>
		/// <returns>子树的文件总数（不判断文件类型）</returns>
		private static int RecursionAllDirectories(string dir, string basedir, ref List<Tuple<string, string>> list) {
			int cnt = 0;
			List<string> children = [.. Directory.EnumerateDirectories(dir)];
			/// 按字符串逻辑排序。资源管理器就是这个顺序，可以使 2.png 排在 10.png 前面，保证图片顺序正确。
			children.Sort(StrCmpLogicalW);
			foreach (string d in children) {
				int dirfilecnt = Directory.EnumerateFiles(d).Count();
				if (dirfilecnt > 0) {
					list.Add(Tuple.Create(basedir, Path.GetRelativePath(basedir, d)));
					cnt += dirfilecnt;
				}
				cnt += RecursionAllDirectories(d, basedir, ref list);
			}
			return cnt;
		}

		[LibraryImport("Shlwapi.dll", EntryPoint = "StrCmpLogicalW", StringMarshalling = StringMarshalling.Utf16)]
		[return: MarshalAs(UnmanagedType.I4)]
		private static partial int StrCmpLogicalW(string psz1, string psz2);
	}
}
