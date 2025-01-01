
namespace PicMerge {
	/// <summary>
	/// 合成器接口
	/// 目前，合成器构造一个只能运行一次。
	/// </summary>
	public interface IMerger : IDisposable {

		/// <summary>
		/// 合并文件。内部串行并行由具体对象决定。
		/// </summary>
		/// <param name="outputfilepath">输出文件路径</param>
		/// <param name="files">输入文件的列表</param>
		/// <param name="title">内定标题</param>
		/// <returns>无法合入的文件的列表</returns>
		public virtual List<string> Process(string outputfilepath, List<string> files, string title = "") {
			return [];
		}

		/// <summary>
		/// 创建一个合成器实例。
		/// </summary>
		/// <param name="filepara">是否文件级并行</param>
		/// <param name="finish1img">完成一个文件的回调</param>
		/// <param name="pageSizeType">页面大小类型</param>
		/// <param name="pagesizex">页面大小宽</param>
		/// <param name="pagesizey">页面大小高</param>
		/// <param name="compress">是否压缩所有图片</param>
		/// <returns>创建的实例</returns>
		public static IMerger Create(bool filepara, Action finish1img, int pageSizeType = 2, int pagesizex = 0, int pagesizey = 0, bool compress = true) {
			return filepara ? new FileParallel(finish1img, pageSizeType, pagesizex, pagesizey, compress) : new Main(finish1img, pageSizeType, pagesizex, pagesizey, compress);
		}

		/// <summary>
		/// 确保给定的目录存在。请在传入前 检查 path是 想要的目录的路径 而不是 想要的文件的路径。
		/// 该方法会 递归地创建链条上的所有目录。例如传入 C:\DirA\DirB\DirC，而 DirA 不存在，
		/// 那么该方法会创建 DirA、DirB、DirC 使输入路径可用。
		/// </summary>
		/// <param name="path">要求的目录</param>
		/// <exception cref="DirectoryNotFoundException">无法完成任务</exception>
		public static void EnsureFolderExisting(string dirpath) {
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
		/// <param name="path">指定的文件目录</param>
		public static void EnsureFileCanExsist(string path) {
			string? folder = Path.GetDirectoryName(path);
			if (folder == null)
				return;
			EnsureFolderExisting(folder);
		}
	}
}
