using static PicMerge.IMerger;

namespace PicMerge {
	internal class MergerArchive(Action finish1img, bool _stayNomove, bool _keepStruct, PageParam pp, ImageParam ip) : Merger(ip), IMerger {

		private readonly PageParam m_pp = pp;
		private readonly ImageParam m_ip = ip;

		private readonly bool m_keepStruct = _keepStruct;
		private readonly bool m_stayNomove = _stayNomove;

		/// <summary>
		/// 完成一张图片（其实是一个文件，不论是否是图片）的回调。
		/// </summary>
		private readonly Action FinishOneImg = finish1img;

		/// <summary>
		/// 合并文件。内部串行并行由具体对象决定。
		/// </summary>
		/// <param name="destnationDir">输出文件路径</param>
		/// <param name="files">输入文件的列表</param>
		/// <param name="title">内定标题</param>
		/// <returns>无法合入的文件的列表</returns>
		public virtual List<FileResult> Process(string destnationDir, List<string> files, string? title = null) {
			List<FileResult> res = [];
			int launchedCnt = 0;

			List<Task<List<FileResult>>> tasks = [];
			for (int i = 0, n = int.Max(Environment.ProcessorCount - 1, 1); i < n && launchedCnt < files.Count; ++i) {
				tasks.Add(ProcessOneArchiveAsync(files[launchedCnt++], destnationDir));
			}

			int landedCnt = 0;
			while (landedCnt < files.Count) {
				int index = Task.WaitAny([.. tasks]);
				Task<List<FileResult>> finishedTask = tasks[index];
				if (launchedCnt < files.Count)
					tasks[index] = ProcessOneArchiveAsync(files[launchedCnt++], destnationDir);
				var finishedRes = finishedTask.Result;
				res.AddRange(finishedRes);
				landedCnt++;
				FinishOneImg();
			}

			return res;
		}

		public Task<List<FileResult>> ProcessOneArchiveAsync(string archivePath, string destDir) {
			return Task.Run(() => { return ProcessOneArchive(archivePath, destDir); });
		}

		public List<FileResult> ProcessOneArchive(string archivePath, string destDir) {
			string outdir = Path.ChangeExtension(archivePath, "[Merged]");
			if (!m_stayNomove)
				outdir = Path.Combine(destDir, Path.GetFileName(outdir));
			EnsureFolderExisting(outdir);
			ArchiveHandler handler = new(m_keepStruct, m_pp, m_ip);
			return handler.Process(outdir, archivePath);
		}
	}
}
