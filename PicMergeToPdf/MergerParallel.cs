using iText.IO.Image;
using static PicMerge.IMerger;

namespace PicMerge {
	/// <summary>
	/// 合成器实现：文件级并行。
	/// 目前，该类构造一个只能运行一次。
	/// </summary>
	/// <param name="finish1img">完成一个文件的回调</param>
	/// <param name="param">参数</param>
	internal class MergerParallel(Action finish1img, Parameters param) : Merger(param), IMerger {

		/// <summary>
		/// 多任务协作时，任务中sleep的 默认 毫秒数。
		/// </summary>
		private const int m_sleepMs = 20;
		/// <summary>
		/// 完成一张图片（其实是一个文件，不论是否是图片）的回调。
		/// </summary>
		private readonly Action FinishOneImg = finish1img;
		/// <summary>
		/// 无法合入的文件的列表。
		/// </summary>
		private readonly List<FailedFile> m_failed = [];

		private class Count(int _v) {
			public int value = _v;
		}
		/// <summary>
		/// 从Process输入的输入文件列表。
		/// </summary>
		private readonly List<string> m_files = [];
		/// <summary>
		/// 加载结果的队列。
		/// </summary>
		private readonly Queue<ImageData?> m_loadedImg = [];
		/// <summary>
		/// 已开始运行过的任务 的 计数。
		/// 用于 每个任务 在最开始 获取 自己该处理哪个文件（m_files[id]）。
		/// </summary>
		private readonly Count m_started = new(0);
		/// <summary>
		/// 已将结果压入队列的任务 的 计数。
		/// 用于 每个任务 确定 是否 轮到自己 把结果入队 了，保证 入队顺序 就是 输入的文件顺序。
		/// </summary>
		private readonly Count m_loaded = new(0);

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
		public virtual List<FailedFile> Process(string outputfilepath, List<string> files, string? title = null) {
			m_files.AddRange(files);

			using PdfTarget pdfTarget = new(outputfilepath, title);
			/// 按电脑核心数启动load，间隔一段时间加入避免同时IO。
			for (int i = 0, n = Environment.ProcessorCount; i < n; i++) {
				LoadAsync();
				Thread.Sleep(m_sleepMs);
			}

			List<ImageData?> imageDatas = [];
			int imgCnt = 0;
			while (true) {
				bool isEmpty = true;
				lock (m_loadedImg) {
					if (m_loadedImg.Count != 0) {
						isEmpty = false;
						while (m_loadedImg.Count > 0) {
							imageDatas.Add(m_loadedImg.Dequeue());
						}
					}
				}
				if (isEmpty) {
					Thread.Sleep(m_sleepMs);
					continue;
				}
				// Add Image.
				foreach (var imageData in imageDatas) {
					if (imageData == null) {
						FinishOneImg();
						imgCnt++;
						continue;
					}
					if (!pdfTarget.AddImage(imageData, ref m_param)) {
						lock (m_failed) {
							m_failed.Add(new FailedFile(0x1001, files[imgCnt], "Unable to add into pdf [iText internal problem]."));
						}
					}
					FinishOneImg();
					imgCnt++;
				}
				imageDatas.Clear();
				if (imgCnt >= files.Count)
					break;
			}

			return m_failed;
		}

		/// <summary>
		/// 开启一个新的加载图片任务。
		/// </summary>
		private async void LoadAsync() {
			await Task.Run(LoadOneProc);
		}

		/// <summary>
		/// 加载一张图片的任务过程。
		/// </summary>
		private void LoadOneProc() {
			/// 取序号。
			int id;
			lock (m_started) {
				if (m_started.value >= m_files.Count)
					return;
				id = m_started.value++;
			}
			string file = m_files[id];

			bool finished = true; // 如果是压缩文件就不压入一个 null，避免计数加一。

			/// 加载并处理。
			ImageData? imageData = null;
			try {
				imageData = ParaImage(file);
			}
			catch (FileType.ArchiveException) {
				lock (m_failed) {
					m_failed.Add(new FailedFile(0x114514, file, "Archive file convertion is not enabled."));
				}
				finished = false;
			}
			catch (Exception ex) {
				lock (m_failed) {
					m_failed.Add(new FailedFile(0x2001, file, $"Failed to load image because: {ex.Message}"));
				}
			}

			/// Add loaded data into queue.
			while (true) {
				lock (m_loaded) {
					/// My turn to enqueue.
					if (id == m_loaded.value) {
						if (finished)
							while (true) {
								lock (m_loadedImg) {
									/// Ensure queue is not too large.
									if (m_loadedImg.Count < Environment.ProcessorCount * 2) {
										m_loadedImg.Enqueue(imageData);
										break;
									}
								}
								Thread.Sleep(m_sleepMs);
							}
						m_loaded.value++;
						break;
					}
				}
				Thread.Sleep(m_sleepMs);
			}

			/// Next Task.
			LoadAsync();

			return;
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
