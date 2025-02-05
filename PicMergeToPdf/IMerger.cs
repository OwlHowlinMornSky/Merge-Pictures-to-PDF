
namespace PicMerge {
	public readonly struct PageParam(
		PageParam.FixedType _fixedType, float _width, float _height
	) {
		[Flags]
		public enum FixedType {
			WidthFixed = 1,
			HeightFixed = 2
		}
		/// <summary>
		/// Describs whice sides is fixed. It can be a "bit-or" combination of followings:
		/// 0x1: width fixed.
		/// 0x2: height fixed.
		/// </summary>
		public readonly FixedType fixedType = _fixedType;
		/// <summary>
		/// This is used when width is fixed.
		/// If this is less than 10, "width fixed" is disabled.
		/// </summary>
		public readonly float width = _width;
		/// <summary>
		/// This is used when height is fixed.
		/// If this is less than 10, "height fixed" is disabled.
		/// </summary>
		public readonly float height = _height;
	}
	public readonly struct ImageParam(
		bool _compress, int _format, int _quality,
		bool _resize, int _width, int _height, int _shortSide, int _longSide,
		bool _reduceBy2
	) {
		/// <summary>
		/// Try compress any image or not.
		/// </summary>
		public readonly bool compress = _compress;
		/// <summary>
		/// Compress target image format.
		/// 0 = NoChange, 1=jpg, 2=png.
		/// </summary>
		public readonly int format = _format;
		/// <summary>
		/// Compress quality. From 0 to 100.
		/// If target format is PNG, this will automatically be mapped from 0~100 into 0~9.
		/// </summary>
		public readonly int quality = _quality;
		/// <summary>
		/// Resize image. Magnify is not allowed.
		/// </summary>
		public readonly bool resize = _resize;
		/// <summary>
		/// Preferred width of result.
		/// </summary>
		public readonly int width = _width;
		/// <summary>
		/// Preferred height of result.
		/// </summary>
		public readonly int height = _height;
		/// <summary>
		/// Preferred length of short side of result.
		/// </summary>
		public readonly int shortSide = _shortSide;
		/// <summary>
		/// Preferred length of long side of result.
		/// </summary>
		public readonly int longSide = _longSide;
		/// <summary>
		/// When reducing image, let the scale be power of 2.
		/// Reducing is goning on until each measure is not great than your preferred value.
		/// For example:
		/// (1400, 600) -> (700, 300) -> (350, 150) ...
		/// </summary>
		public readonly bool reduceByPowOf2 = _reduceBy2;
	}

	/// <summary>
	/// 合成器接口
	/// 目前，合成器构造一个只能运行一次。
	/// </summary>
	public interface IMerger : IDisposable {

		/// <summary>
		/// 内存映射文件设定的最大大小。
		/// </summary>
		internal const long MapFileSize = 0x04000000;

		public readonly struct FileResult(uint _c, string _file, string _desc = "Success.") {
			public readonly uint code = _c;
			public readonly string filename = _file;
			public readonly string? description = _desc;
		}

		/// <summary>
		/// 合并文件。内部串行并行由具体对象决定。
		/// </summary>
		/// <param name="outputfilepath">输出文件路径</param>
		/// <param name="files">输入文件的列表</param>
		/// <param name="title">内定标题</param>
		/// <returns>无法合入的文件的列表</returns>
		public Task<List<FileResult>> ProcessAsync(string outputfilepath, List<string> files, string? title = null) {
			return Task.Run(() => { return Process(outputfilepath, files, title); });
		}

		/// <summary>
		/// 合并文件。内部串行并行由具体对象决定。
		/// </summary>
		/// <param name="outputfilepath">输出文件路径</param>
		/// <param name="files">输入文件的列表</param>
		/// <param name="title">内定标题</param>
		/// <returns>无法合入的文件的列表</returns>
		public List<FileResult> Process(string outputfilepath, List<string> files, string? title = null);

		/// <summary>
		/// 创建一个合成器实例。
		/// </summary>
		/// <param name="parallel">是否文件级并行</param>
		/// <param name="finish1img">完成一个文件的回调</param>
		/// <param name="pp"></param>
		/// <param name="ip"></param>
		/// <returns>创建的实例</returns>
		public static IMerger Create(
			bool parallel,
			Action finish1img,
			PageParam pp,
			ImageParam ip
		) {
			return parallel ? new MergerParallel(finish1img, pp, ip) : new MergerSerial(finish1img, pp, ip);
		}

		public static IMerger CreateArchiveConverter(
			bool keepStruct,
			PageParam pp,
			ImageParam ip
		) {
			return new MergerArchive(keepStruct, pp, ip);
		}

		/// <summary>
		/// 确保给定的目录存在。请在传入前 检查 path是 想要的目录的路径 而不是 想要的文件的路径。
		/// 该方法会 递归地创建链条上的所有目录。例如传入 C:\DirA\DirB\DirC，而 DirA 不存在，
		/// 那么该方法会创建 DirA、DirB、DirC 使输入路径可用。
		/// </summary>
		/// <param name="dirpath">要求的目录路径</param>
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

		/// <summary>
		/// 枚举可用的文件名。防止输出文件名与现有文件相同导致覆盖。
		/// 但是扫描可用文件名与开始写入有一定时间差，
		/// 如果用户~脑残到~在这段时间创建同名文件，可能会出问题。
		/// </summary>
		/// <param name="dir">文件将所处的目录</param>
		/// <param name="stem">文件的期望名称</param>
		/// <param name="exname">文件的扩展名</param>
		/// <returns>添加可能的" (%d)"后，不与现有文件同名的文件路径</returns>
		internal static string EnumFileName(string dir, string stem, string exname) {
			string res = Path.Combine(dir, stem + exname);
			int i = 0;
			while (File.Exists(res)) {
				i++;
				res = Path.Combine(dir, $"{stem} ({i}){exname}");
			}
			return res;
		}
	}
}
