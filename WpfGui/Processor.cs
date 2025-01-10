using System.IO;
using System.Windows;
using System.Runtime.InteropServices;

namespace WpfGui {
	/// <summary>
	/// 介于 图形界面 和 合成器 之间的 处理器。
	/// </summary>
	/// <param name="guiMain">对应的界面的窗口</param>
	/// <param name="setBarNum">设置进度条进度的回调</param>
	/// <param name="setBarFinish">设置进度条完成的回调</param>
	internal partial class Processor(Window guiMain, Action<int, int> setBarNum, Action setBarFinish) {

		internal struct Parameters(
			bool _recursion = true,
			bool _keepStruct = true,
			bool _stayNoMove = false,
			bool _compress = true,
			int _pageSizeType = 2,
			int _pagesizex = 0,
			int _pagesizey = 0,
			bool _parallelOnFileLevel = true,
			int _type = 1,
			int _quality = 80
		) {
			/// <summary>
			/// 递归输入文件夹。
			/// </summary>
			public bool recursion = _recursion;
			/// <summary>
			/// 保持目录结构输出。
			/// </summary>
			public bool keepStruct = _keepStruct;
			/// <summary>
			/// 压缩所有图片。
			/// </summary>
			public bool compress = _compress;
			/// <summary>
			/// 输出到原位。
			/// </summary>
			public bool stayNoMove = _stayNoMove;
			/// <summary>
			/// 页面大小类型，详见MainWindow。
			/// </summary>
			public int pageSizeType = _pageSizeType;
			/// <summary>
			/// 页面大小宽，详见MainWindow。
			/// </summary>
			public int pagesizex = _pagesizex;
			/// <summary>
			/// 页面大小高，详见MainWindow。
			/// </summary>
			public int pagesizey = _pagesizey;
			/// <summary>
			/// 文件级并行，即此对象的子任务不并发（一个一个执行），但下面的 读取并处理图片 的操作并行。
			/// </summary>
			public bool parallelOnFileLevel = _parallelOnFileLevel;

			public int compressType = _type;
			public int compressQuality = _quality;

			public bool keepPdfInFolder = false;
		}

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

		/// <summary>
		/// 对应的主窗口。用来弹消息框。
		/// </summary>
		private readonly Window m_guiMain = guiMain;
		/// <summary>
		/// 修改进度条进度 的 回调。
		/// </summary>
		private readonly Action<int, int> SetBarNum = setBarNum;
		/// <summary>
		/// 修改进度条为任务完成 的 回调。
		/// </summary>
		private readonly Action SetBarFinish = setBarFinish;

		/// <summary>
		/// 合成参数。
		/// </summary>
		private Parameters m_param;

		/// <summary>
		/// 主任务。
		/// </summary>
		private Task? m_taskStem;
		/// <summary>
		/// 防止多个线程同时start的锁。
		/// </summary>
		private readonly object m_lockForStart = new();

		/// <summary>
		/// 选定的输出目录，若输出到原位，则为empty。
		/// </summary>
		private string m_destinationDir = "";
		/// <summary>
		/// 待处理的数据。
		/// </summary>
		private readonly Queue<TaskInputData> m_waitings = [];
		/// <summary>
		/// 图片总计数。
		/// </summary>
		private int m_totalImgCnt = 0;
		/// <summary>
		/// 报告完成的图片计数。
		/// </summary>
		private readonly Count m_finishedImg = new(0);
		/// <summary>
		/// 记录未完成的任务数。
		/// </summary>
		private readonly Count m_unfinishedTask = new(0);

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
			lock (m_unfinishedTask) {
				m_unfinishedTask.value--;
			}
			lock (m_waitings) {
				if (m_waitings.Count > 0) {
					ProcessAsync(m_waitings.Dequeue());
					return;
				}
			}
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
			lock (m_waitings) {
				return (m_waitings.Count != 0) || (m_unfinishedTask.value > 0) || (m_taskStem != null && !m_taskStem.IsCompleted);
			}
		}

		/// <summary>
		/// 开始处理。
		/// </summary>
		/// <param name="paths">要处理的文件/文件夹</param>
		/// <returns>是否成功开始</returns>
		internal bool Start(string[] paths, Parameters param) {
			m_param = param;
			lock (m_lockForStart) {
				if (IsRunning()) {
					return false;
				}
				m_taskStem = Task.Run(() => { ProcessStem(paths); return; });
				return true;
			}
		}

		/// <summary>
		/// 主任务过程。主任务预处理输入的路径，初步统计，并发配给子任务。
		/// </summary>
		/// <param name="paths">要处理的文件/文件夹</param>
		private void ProcessStem(string[] paths) {
			bool onlyArchive = false;
			m_destinationDir = "";
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
					/// 预处理任务，包括 可能的【询问输出目录】 和 初步扫描统计。
					var taskScan = Task.Run(() => {
						return ScanInput(paths, ref files, ref directories, ref unknown);
					});
					if (!m_param.stayNoMove) {
						var taskQDest = AskForDestinationAsync(paths[0]);
						taskQDest.Wait();
						if (taskQDest.Result == null)
							return;
						m_destinationDir = taskQDest.Result;
					}
					taskScan.Wait();

					/// 设置统计值。
					m_finishedImg.value = 0;
					m_totalImgCnt = taskScan.Result;
					if (m_totalImgCnt < 1)
						return;

					SetBarNum(0, m_totalImgCnt);
				}

				/// 准备子任务。
				m_waitings.Clear();
				/// 报告无法处理的列表。
				if (unknown.Count > 0) {
					string msg = App.Current.FindResource("CannotProcess").ToString() ?? "Cannot process:";
					foreach (string str in unknown) {
						msg += "\r\n";
						msg += str;
					}
					Task.Run(() => {
						App.Current.Dispatcher.Invoke(() => {
							MessageBox.Show(
								m_guiMain,
								msg,
								$"{m_guiMain.Title}: {App.Current.FindResource("Warning")}",
								MessageBoxButton.OK,
								MessageBoxImage.Warning
							);
						});
					});
				}
				/// 文件夹。
				if (directories.Count > 0) {
					foreach (Tuple<string, string> pair in directories)
						m_waitings.Enqueue(new TaskInputData(TaskInputData.Type.Directory, [pair.Item1, pair.Item2]));
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
						files.Sort(StrCmpLogicalW);
						foreach (string archivePath in files) {
							m_waitings.Enqueue(new TaskInputData(TaskInputData.Type.Archive, [archivePath]));
						}
						onlyArchive = true;
					}
					else {
						m_waitings.Enqueue(new TaskInputData(TaskInputData.Type.FileNotArchive, files));
					}
				}
			}
			m_unfinishedTask.value = m_waitings.Count;
			/// 文件级并行时，只并发2个子任务，避免1个PDF写入时全部等待。
			lock (m_waitings) {
				int i = 0;
				int n = !onlyArchive && m_param.parallelOnFileLevel ? 2 : int.Max(2, Environment.ProcessorCount);
				for (; i < n; i++) {
					if (m_waitings.Count < 1)
						break;
					ProcessAsync(m_waitings.Dequeue());
					Thread.Sleep(50);
				}
			}
		}

		private async void ProcessAsync(TaskInputData data) {
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
					dstDir = Path.Combine(m_destinationDir, relative);
					if (!m_param.keepPdfInFolder && !relativeIsEmpty)
						dstDir = Path.GetDirectoryName(dstDir) ?? dstDir;
				}
				else {
					dstDir = m_destinationDir;
				}

				string outputPath = EnumFileName(dstDir, m_param.keepPdfInFolder ? "Images" : Path.GetFileName(srcDir), ".pdf");

				/// 文件夹对应标题 取 它与基准路径相差的相对路径。
				await ProcessOneFolderAsync(srcDir, outputPath, relativeIsEmpty ? Path.GetFileName(baseDir) : relative);
				break;
			}
			case TaskInputData.Type.FileNotArchive: {
				string pathOfFirstFile = data.files[0];
				string dirOfFirstFile = Path.GetDirectoryName(pathOfFirstFile) ?? "";

				/// 输出到原位时，就是输入路径的父目录，否则，就是选定的目标目录。
				string outputPath = EnumFileName(
					m_param.stayNoMove ? dirOfFirstFile : m_destinationDir,
					"Images", ".pdf"
				);

				/// 零散文件的标题 取 文件所在父目录的名字。
				await ProcessFilesAsync(data.files, outputPath, Path.GetFileName(dirOfFirstFile));
				break;
			}
			case TaskInputData.Type.Archive: {
				string archivePath = data.files[0];
				string outdir = Path.ChangeExtension(archivePath, "[Merged]");
				if (!m_param.stayNoMove)
					outdir = Path.Combine(m_destinationDir, Path.GetFileName(outdir));
				await ProcessArchiveAsync(archivePath, outdir);
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
		private async Task ProcessOneFolderAsync(string sourceDir, string outputPath, string? title) {
			List<string> files = Directory.EnumerateFiles(sourceDir).ToList();
			await ProcessFilesAsync(files, outputPath, title);
		}

		/// <summary>
		/// 子任务过程：处理零散文件。
		/// </summary>
		/// <param name="files">文件列表</param>
		/// <param name="outputPath">输出文件</param>
		/// <param name="title">内定标题（不是文件名）</param>
		private async Task ProcessFilesAsync(List<string> files, string outputPath, string? title) {
			/// 按字符串逻辑排序。资源管理器就是这个顺序，可以使 2.png 排在 10.png 前面，保证图片顺序正确。
			files.Sort(StrCmpLogicalW);
			using PicMerge.IMerger merger = PicMerge.IMerger.Create(
				m_param.parallelOnFileLevel,
				CallbackFinishOneImgFile,
				m_param.pageSizeType,
				m_param.pagesizex,
				m_param.pagesizey,
				m_param.compress,
				m_param.compressType,
				m_param.compressQuality
			);
			List<PicMerge.IMerger.FileResult> result = await merger.ProcessAsync(outputPath, files, title);
			CallbackFinishAllImgFile();
			CheckResultListFailed(title ?? outputPath, ref result);
		}

		private async Task ProcessArchiveAsync(string filePath, string outputPath) {
			using var merger = PicMerge.IMerger.CreateArchiveConverter(
				m_param.keepStruct,
				m_param.pageSizeType,
				m_param.pagesizex,
				m_param.pagesizey,
				m_param.compress,
				m_param.compressType,
				m_param.compressQuality
			);
			List<PicMerge.IMerger.FileResult> failed = await merger.ProcessAsync(outputPath, [filePath]);
			CallbackFinishOneImgFile();
			CallbackFinishAllImgFile();
			CheckResultListFailed(outputPath, ref failed);
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
		/// 询问目标目录。
		/// </summary>
		/// <param name="defpath">初始目录</param>
		/// <returns>选择的目录，或者 null 表示取消</returns>
		private async Task<string?> AskForDestinationAsync(string defpath) {
			return await Task.Run(() => {
				string? res = null;
				App.Current.Dispatcher.Invoke(() => {
					// Configure open folder dialog box
					Microsoft.Win32.OpenFolderDialog dialog = new() {
						Multiselect = false,
						Title = $"{m_guiMain.Title}: {App.Current.FindResource("ChooseDestinationDir").ToString() ?? "output location"}",
						DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
						InitialDirectory = Path.GetDirectoryName(defpath)
					};

					// Show open folder dialog box
					// Process open folder dialog box results
					if (dialog.ShowDialog(m_guiMain) == true) {
						// Get the selected folder
						res = dialog.FolderName;
					}
				});
				return res;
			});
		}

		/// <summary>
		/// 子任务步骤：弹窗报告失败的文件。
		/// </summary>
		/// <param name="title">内定标题</param>
		/// <param name="result">结果列表</param>
		private void CheckResultListFailed(string title, ref List<PicMerge.IMerger.FileResult> result) {
			var failed = result.Where(r => r.code > 0x80000000);
			if (failed.Any()) {
				string msg = string.Format(App.Current.FindResource("CannotMerge").ToString() ?? "Failed to merge into {0}:", title);
				foreach (var str in failed) {
					msg += $"\r\n<{str.filename}> {str.description}";
				}
				App.Current.Dispatcher.Invoke(() => {
					MessageBox.Show(
						m_guiMain,
						msg,
						$"{m_guiMain.Title}: {App.Current.FindResource("Warning")}",
						MessageBoxButton.OK,
						MessageBoxImage.Warning
					);
				});
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
