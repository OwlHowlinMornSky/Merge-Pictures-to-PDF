using iText.IO.Image;
using static PicMerge.IMerger;

namespace PicMerge {
	/// <summary>
	/// 合成器实现：文件级并行。
	/// 目前，该类构造一个只能运行一次。
	/// </summary>
	/// <param name="finish1img">完成一个文件的回调</param>
	/// <param name="param">参数</param>
	/// <err frag="0x8002" ack="0003"></err>
	internal class MergerParallel(Action finish1img, PageParam pp, ImageParam ip) : Merger(ip), IMerger {

		private readonly PageParam m_pp = pp;

		/// <summary>
		/// 多任务协作时，任务中sleep的 默认 毫秒数。
		/// </summary>
		private const int m_sleepMs = 20;
		/// <summary>
		/// 完成一张图片（其实是一个文件，不论是否是图片）的回调。
		/// </summary>
		private readonly Action FinishOneImg = finish1img;

		~MergerParallel() {
			Dispose(false);
		}

		/// <summary>
		/// 合并文件。此方法文件级并行，即并发加载处理，再依次加入结果。
		/// </summary>
		/// <param name="outputfilepath">输出文件路径</param>
		/// <param name="files">输入文件的列表</param>
		/// <param name="title">内定标题</param>
		/// <returns>无法合入的文件的列表</returns>
		public virtual List<FileResult> Process(string outputfilepath, List<string> files, string? title = null) {
			List<FileResult> result = [];
			Queue<Task<ImageData?>> tasks = [];

			int launchedCnt = 0;
			/// 按电脑核心数启动load（由于上层发起两个任务，因此减半），间隔一段时间加入避免同时IO。
			for (int i = 0, n = Environment.ProcessorCount / 2; i < n && launchedCnt < files.Count; i++) {
				tasks.Enqueue(ParaLoad(files[launchedCnt++]));
				Thread.Sleep(m_sleepMs);
			}

			using PdfTarget pdfTarget = new(outputfilepath, title);
			int landedCnt = 0;
			while (landedCnt < files.Count) {
				tasks.Peek().Wait();
				if (launchedCnt < files.Count)
					tasks.Enqueue(ParaLoad(files[launchedCnt++]));
				ImageData? imageData = tasks.Dequeue().Result;
				/// Add Image.
				string file = files[landedCnt++];
				if (imageData == null) {
					result.Add(new FileResult(0x80020001, file, StrUnsupported));
				}
				else if (!pdfTarget.AddImage(imageData, in m_pp)) {
					result.Add(new FileResult(0x80020002, file, StrFailedToAdd));
				}
				else {
					result.Add(new FileResult(0x1, file));
				}
				FinishOneImg();
			}

			return result;
		}

		/// <summary>
		/// 开启一个新的加载图片任务。
		/// </summary>
		private Task<ImageData?> ParaLoad(string filepath) {
			return Task.Run(() => { return ParaImage(filepath); });
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool m_disposed = false;
		protected virtual void Dispose(bool disposing) {
			if (m_disposed)
				return;
			m_disposed = true;
		}
	}
}
