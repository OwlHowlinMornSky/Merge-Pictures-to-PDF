using iText.IO.Image;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.IO.MemoryMappedFiles;
using static PicMerge.IMerger;

namespace PicMerge {
	/// <summary>
	/// 合成器实现：文件级并行。
	/// 目前，该类构造一个只能运行一次。
	/// </summary>
	/// <param name="finish1img">完成一个文件的回调</param>
	/// <param name="pageSizeType">页面大小类型</param>
	/// <param name="pagesizex">页面大小宽</param>
	/// <param name="pagesizey">页面大小高</param>
	/// <param name="compress">是否压缩所有图片</param>
	internal class MergerParallel(Action finish1img, Parameters param) : Merger(param), IMerger {

		/// <summary>
		/// 完成一张图片（其实是一个文件，不论是否是图片）的回调。
		/// </summary>
		private readonly Action FinishOneImg = finish1img;

		/// <summary>
		/// 从Process输入的输入文件列表。
		/// </summary>
		private List<string> m_files = [];

		/// <summary>
		/// 已开始运行过的任务 的 计数。
		/// 用于 每个任务 在最开始 获取 自己该处理哪个文件（m_files[id]）。
		/// </summary>
		private int m_startedCnt = 0;
		/// <summary>
		/// 已开始运行过的任务计数 的 锁。
		/// </summary>
		private readonly object m_startedCntLock = new();
		/// <summary>
		/// 已将结果压入队列的任务 的 计数。
		/// 用于 每个任务 确定 是否 轮到自己 把结果入队 了，保证 入队顺序 就是 输入的文件顺序。
		/// </summary>
		private int m_loadedCnt = 0;
		/// <summary>
		/// 已将结果压入队列的任务计数 的 锁。
		/// </summary>
		private readonly object m_loadedCntLock = new();

		/// <summary>
		/// 加载结果的队列。
		/// </summary>
		private readonly Queue<ImageData?> m_loadedImg = [];
		/// <summary>
		/// 多任务协作时，任务中sleep的 默认 毫秒数。
		/// </summary>
		private const int m_sleepMs = 20;

		/// <summary>
		/// 无法合入的文件的列表。
		/// </summary>
		private readonly List<FailedFile> m_failed = [];

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
			m_files = files;

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

			//if (!pdfTarget.IsUsed()) // 一个都没法合成的话返回空。
			//	m_failed.Clear();

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
			lock (m_startedCntLock) {
				if (m_startedCnt >= m_files.Count)
					return;
				id = m_startedCnt++;
			}
			string file = m_files[id];
			ImageData? imageData = null;
			try {
				/// 加载并处理。
				imageData = ParaImage(file);
			}
			catch (FileType.ArchiveException) {
				lock (m_failed) {
					m_failed.Add(new FailedFile(0x114514, file, "Archive file convertion is not enabled."));
				}
			}
			catch (Exception ex) {
				lock (m_failed) {
					m_failed.Add(new FailedFile(0x2001, file, $"Failed to load image because: {ex.Message}"));
				}
			}

			/// Add loaded data into queue.
			while (true) {
				lock (m_loadedCntLock) {
					/// My turn to enqueue.
					if (id == m_loadedCnt) {
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
						m_loadedCnt++;
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
