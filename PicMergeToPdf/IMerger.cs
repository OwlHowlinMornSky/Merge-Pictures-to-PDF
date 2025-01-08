
using iText.Kernel.Pdf;

namespace PicMerge {
	/// <summary>
	/// 合成器接口
	/// 目前，合成器构造一个只能运行一次。
	/// </summary>
	public interface IMerger : IDisposable {

		/// <summary>
		/// 内存映射文件设定的最大大小。
		/// </summary>
		internal const long MapFileSize = 0x04000000;

		internal struct Parameters(
			int _pageSizeType = 2, int _pagesizex = 0, int _pagesizey = 0,
			bool _compress = true, int _type = 1, int _quality = 80,
			bool _archive = true
		) {
			/// <summary>
			/// 页面大小类型。
			/// </summary>
			public readonly int pageSizeType = _pageSizeType;
			/// <summary>
			/// 页面大小宽。使用第一张图片的尺寸时需要修改，所以不能只读。
			/// </summary>
			public float pagesizex = _pagesizex;
			/// <summary>
			/// 页面大小高。使用第一张图片的尺寸时需要修改，所以不能只读。
			/// </summary>
			public float pagesizey = _pagesizey;
			/// <summary>
			/// 是否压缩所有图片。
			/// </summary>
			public readonly bool compress = _compress;

			public int compressType = _type;
			public int compressQuality = _quality;
			public bool convertArchive = _archive;
		}

		public readonly struct FailedFile(int _c, string _file, string _desc) {
			public readonly int code = _c;
			public readonly string filename = _file;
			public readonly string description = _desc;
		}

		/// <summary>
		/// 合并文件。内部串行并行由具体对象决定。
		/// </summary>
		/// <param name="outputfilepath">输出文件路径</param>
		/// <param name="files">输入文件的列表</param>
		/// <param name="title">内定标题</param>
		/// <returns>无法合入的文件的列表</returns>
		public Task<List<FailedFile>> ProcessAsync(string outputfilepath, List<string> files, string? title = null) {
			return Task.Run(() => { return Process(outputfilepath, files, title); });
		}

		/// <summary>
		/// 合并文件。内部串行并行由具体对象决定。
		/// </summary>
		/// <param name="outputfilepath">输出文件路径</param>
		/// <param name="files">输入文件的列表</param>
		/// <param name="title">内定标题</param>
		/// <returns>无法合入的文件的列表</returns>
		public virtual List<FailedFile> Process(string outputfilepath, List<string> files, string? title = null) {
			return [];
		}

		/// <summary>
		/// 创建一个合成器实例。
		/// </summary>
		/// <param name="parallel">是否文件级并行</param>
		/// <param name="finish1img">完成一个文件的回调</param>
		/// <param name="pageSizeType">页面大小类型</param>
		/// <param name="pagesizex">页面大小宽</param>
		/// <param name="pagesizey">页面大小高</param>
		/// <param name="compress">是否压缩所有图片</param>
		/// <returns>创建的实例</returns>
		public static IMerger Create(
			bool parallel,
			Action finish1img,
			int pageSizeType = 2,
			int pagesizex = 0,
			int pagesizey = 0,
			bool compress = true,
			int type = 1,
			int quality = 80,
			bool archive = true) {
			return parallel ?
				new MergerParallel(finish1img, new Parameters(pageSizeType, pagesizex, pagesizey, compress, type, quality, archive)) :
				new MergerSerial(finish1img, new Parameters(pageSizeType, pagesizex, pagesizey, compress, type, quality, archive));
		}

		/// <summary>
		/// 确保给定的目录存在。请在传入前 检查 path是 想要的目录的路径 而不是 想要的文件的路径。
		/// 该方法会 递归地创建链条上的所有目录。例如传入 C:\DirA\DirB\DirC，而 DirA 不存在，
		/// 那么该方法会创建 DirA、DirB、DirC 使输入路径可用。
		/// </summary>
		/// <param name="path">要求的目录路径</param>
		/// <exception cref="DirectoryNotFoundException">无法完成任务</exception>
		internal static void EnsureFolderExisting(string dirpath) {
			if (Directory.Exists(dirpath))
				return;
			string parent = Path.GetDirectoryName(dirpath) ??
				throw new DirectoryNotFoundException($"Parent of \"{dirpath}\" is not exist!");
			EnsureFolderExisting(parent);
			Directory.CreateDirectory(dirpath);
			return;
		}

		/// <summary>
		/// 确保文件可存在（即父目录存在）。
		/// 递归地创建所有祖先目录。
		/// </summary>
		/// <param name="path">指定的路径</param>
		internal static void EnsureFileCanExsist(string path) {
			string? folder = Path.GetDirectoryName(path);
			if (folder == null)
				return;
			EnsureFolderExisting(folder);
		}
	}
}
