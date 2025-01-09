using iText.IO.Image;
using static PicMerge.IMerger;

namespace PicMerge {
	/// <summary>
	/// 合成器实现：普通串行。
	/// 目前，该类构造一个只能运行一次。
	/// </summary>
	/// <param name="finish1img">完成一个文件的回调</param>
	/// <param name="param">参数</param>
	internal class MergerSerial(Action finish1img, Parameters param) : Merger(param), IMerger {

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
		/// <returns>无法合入的文件的列表</returns>
		public virtual List<FailedFile> Process(string outputfilepath, List<string> files, string? title = null) {
			/// 无法合入的文件的列表。
			List<FailedFile> failed = [];
			try {
				using PdfTarget pdfTarget = new(outputfilepath, title);

				/// 先扫到可以处理的文件。
				int i = 0;
				ImageData? imageData = null;
				for (; i < files.Count; ++i) {
					string file = files[i];

					try {
						imageData = LoadImage(file, m_compressTarget);
					}
					catch (Exception ex) {
						failed.Add(new FailedFile(0x2001, file, $"Failed to load image because: {ex.Message}"));
						FinishOneImg();
						continue;
					}

					if (imageData != null) {
						break;
					}
					failed.Add(new FailedFile(0x3001, file, "Unsupported type."));
					FinishOneImg();
				}
				if (imageData == null) {
					return failed;
				}
				/// 再打开文件开写。这样的话，如果没有可合入的文件，就不会创建出pdf。
				if (!pdfTarget.AddImage(imageData, ref m_param)) {
					failed.Add(new FailedFile(0x4001, files[i], "Unable to add into pdf [iText internal problem]."));
				}
				FinishOneImg();
				for (++i; i < files.Count; ++i) {
					string file = files[i];

					try {
						imageData = LoadImage(file, m_compressTarget);
					}
					catch (Exception ex) {
						failed.Add(new FailedFile(0x2001, file, $"Failed to load image because: {ex.Message}"));
						FinishOneImg();
						continue;
					}

					if (imageData == null) {
						failed.Add(new FailedFile(0x3001, file, "Unsupported type."));
						FinishOneImg();
						continue;
					}
					if (!pdfTarget.AddImage(imageData, ref m_param)) {
						failed.Add(new FailedFile(0x4001, files[i], "Unable to add into pdf [iText internal problem]."));
					}
					FinishOneImg();
				}
			}
			catch (Exception ex) {
				failed = [new FailedFile(0x4002, ex.Message, ex.Source ?? "")];
			}
			return failed;
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

