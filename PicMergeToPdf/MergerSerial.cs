using iText.IO.Image;
using static PicMerge.IMerger;

namespace PicMerge {
	/// <summary>
	/// 合成器实现：普通串行。
	/// 目前，该类构造一个只能运行一次。
	/// </summary>
	/// <param name="finish1img">完成一个文件的回调</param>
	/// <param name="param">参数</param>
	/// <err frag="0x8001" ack="0006"></err>
	internal class MergerSerial(Action finish1img, PageParam pp, ImageParam ip) : Merger(ip), IMerger {

		private readonly PageParam m_pp = pp;

		/// <summary>
		/// 完成一张图片（其实是一个文件，不论是否是图片）的回调。
		/// </summary>
		private readonly Action FinishOneImg = finish1img;

		/// <summary>
		/// 用于接受压缩结果。
		/// </summary>
		private readonly CompressTarget m_compressTarget = new();

		~MergerSerial() {
			Dispose(false);
		}

		/// <summary>
		/// 合并文件。此方法文件级串行，即依次读取并处理图片。
		/// </summary>
		/// <param name="outputfilepath">输出文件路径</param>
		/// <param name="files">输入文件的列表</param>
		/// <param name="title">内定标题</param>
		/// <returns>合入文件之结果之列表</returns>
		public virtual List<FileResult> Process(string outputfilepath, List<string> files, string? title = null) {
			List<FileResult> result = [];
			try {
				using PdfTarget pdfTarget = new(outputfilepath, title);

				/// 先扫到可以处理的文件。
				int i = 0;
				ImageData? imageData = null;
				for (; i < files.Count; ++i) {
					string file = files[i];

					imageData = LoadImage(file, m_compressTarget);
					if (imageData != null) {
						break;
					}
					result.Add(new FileResult(0x80010001, file, StrUnsupported));
					FinishOneImg();
				}
				if (imageData == null) {
					return result;
				}

				/// 再打开文件开写。这样的话，如果没有可合入的文件，就不会创建出pdf。
				if (pdfTarget.AddImage(imageData, in m_pp)) {
					result.Add(new FileResult(0x1, files[i]));
				}
				else {
					result.Add(new FileResult(0x80010002, files[i], StrFailedToAdd));
				}
				FinishOneImg();

				for (++i; i < files.Count; ++i) {
					string file = files[i];

					imageData = LoadImage(file, m_compressTarget);

					if (imageData == null) {
						result.Add(new FileResult(0x80010003, file, StrUnsupported));
					}
					else if (!pdfTarget.AddImage(imageData, in m_pp)) {
						result.Add(new FileResult(0x80010004, file, StrFailedToAdd));
					}
					else {
						result.Add(new FileResult(0x1, file));
					}

					FinishOneImg();
				}
			}
			catch (Exception ex) {
				result.Add(new FileResult(0x80010005, ex.Source ?? "", ex.Message));
			}
			return result;
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool m_disposed = false;
		protected virtual void Dispose(bool disposing) {
			if (m_disposed)
				return;
			if (disposing) {
				m_compressTarget.Dispose();
			}
			m_disposed = true;
		}
	}
}

