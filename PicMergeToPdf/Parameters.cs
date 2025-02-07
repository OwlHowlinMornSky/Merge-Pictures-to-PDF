
namespace PicMerge {
	public struct IOParam(
		bool _recursion,
		bool _keepStruct,
		bool _stayNoMove,
		string _targetPath
	) {
		/// <summary>
		/// 递归输入文件夹。
		/// </summary>
		public bool recursion = _recursion;
		/// <summary>
		/// 保持目录结构输出。
		/// </summary>
		public bool keepStruct = _keepStruct;
		/// <summary>
		/// 输出到原位。
		/// </summary>
		public bool stayNoMove = _stayNoMove;
		/// <summary>
		/// 把合成的PDF放在和图片同样的目录级别中。即设为true时，不把PDF放在目录并排位置。
		/// </summary>
		public bool keepPdfInFolder = false;
		/// <summary>
		/// 当 stayNoMove 为false时，此项必须包含目标目录。
		/// </summary>
		public string destinationPath = _targetPath;
	}
	public readonly struct PageParam(
		PageParam.FixedType _fixedType, float _width, float _height, uint _dpi
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
		/// <summary>
		/// It is used to scale page.
		/// The greater the page smaller.
		/// </summary>
		public readonly uint dpi = _dpi;
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
}
