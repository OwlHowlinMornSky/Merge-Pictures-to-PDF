using iText.IO.Image;
using static PicMerge.IMerger;

namespace PicMerge {
	/// <summary>
	/// 合成器实现：文件级并行。
	/// 目前，该类构造一个只能运行一次。
	/// </summary>
	/// <param name="finish1img">完成一个文件的回调</param>
	/// <param name="pp">页面参数</param>
	/// <param name="ip">图片参数</param>
	/// <err frag="0x8002" ack="0003"></err>
	internal class MergerParallel(Action finish1img, PageParam pp, ImageParam ip) : Merger(ip), IMerger {

		private readonly PageParam m_pp = pp;

		/// <summary>
		/// 完成一张图片（其实是一个文件，不论是否是图片）的回调。
		/// </summary>
		private readonly Action FinishOneImg = finish1img;

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
			/// 按电脑核心数启动load。
			for (int i = 0, n = Environment.ProcessorCount + 1; i < n && launchedCnt < files.Count; i++) {
				tasks.Enqueue(ParaLoad(files[launchedCnt++]));
			}

			using PdfTarget pdfTarget = new(outputfilepath, title);
			int landedCnt = 0;
			while (landedCnt < files.Count) {
				tasks.Peek().Wait();
				if (launchedCnt < files.Count) {
					tasks.Enqueue(ParaLoad(files[launchedCnt++]));
				}
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
			return Task.Run(() => { return LoadImage(filepath); });
		}
	}
}
