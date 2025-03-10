using iText.IO.Image;

namespace PicMerge {
	internal class Merger(ImageParam ip) {

		protected string StrUnsupported = "Unsupported type.";
		protected string StrFailedToAdd = "Failed to add into pdf.";

		protected readonly ImageParam m_param = ip;
		protected readonly object m_lock = new(); // Used to avoid IO at the same time.

		/// <summary>
		/// 用于从文件加载图片。
		/// </summary>
		/// <param name="filepath">图片文件路径</param>
		/// <returns>加载结果</returns>
		internal ImageData? LoadImage(string filepath) {
			ImageData? res;
			try {
				using FileStream inputStream = new(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
				res = ReadImage(inputStream);
			}
			catch (Exception ex) {
				Logger.Log($"[LoadImage Exception]\nPath: \'{filepath}\'\nMessage: {ex.Message}.");
				return null;
			}
			if (res == null) {
				Logger.Log($"[LoadImage Failed] Path: \'{filepath}\'.");
			}
			return res;
		}

		/// <summary>
		/// 从内存映射文件读取图片。
		/// </summary>
		/// <param name="instream">要读入之图片之文件流</param>
		/// <returns>加载结果</returns>
		internal ImageData? ReadImage(Stream instream) {
			try {
				if (instream.Length > int.MaxValue || instream.Length < 8) {
					return null;
				}
				FileType.Type type;
				byte[] inbuffer;
				lock (m_lock) {
					type = FileType.CheckType(instream);
					if (type == FileType.Type.Unknown) {
						return null;
					}
					instream.Seek(0, SeekOrigin.Begin);
					inbuffer = new byte[instream.Length];
					instream.ReadExactly(inbuffer, 0, (int)instream.Length);
				}
				return m_param.compress ? LoadImageInMemory_Compress(type, ref inbuffer) : LoadImageInMemory_Direct(type, ref inbuffer);
			}
			catch (Exception ex) {
				Logger.Log($"[ReadImage Exception]: {ex.Message}.");
				return null;
			}
		}

		/// <summary>
		/// 压缩全部图片时的加载逻辑。
		/// </summary>
		/// <param name="inFile">欲加载之文件</param>
		/// <param name="compt">压缩器</param>
		/// <returns>加载出的数据，或者 null 若无法加载</returns>
		protected ImageData? LoadImageInMemory_Compress(FileType.Type type, ref byte[] inbuffer) {
			ImageData? imageData;
			switch (type) {
			case FileType.Type.JPEG: // CSI, Img#, Direct.
			case FileType.Type.PNG:  // CSI, Img#, Direct.
			case FileType.Type.TIFF: // CSI, Img#, Direct.
			case FileType.Type.WEBP: // CSI, Img#.
				/// 尝试利用 Caesium-Iodine 压缩
				try {
					byte[] outbuffer = CompressTarget.GetCompressedImageData(ref inbuffer, m_param);
					imageData = ImageDataFactory.Create(outbuffer);
					imageData.SetDpi(4096, 4096);
				}
				catch (Exception ex) {
					Logger.Log($"[Iodine Exception]: {ex.Message}.");
					goto case FileType.Type.GIF;
				}
				break;
			case FileType.Type.BMP:  // Img#, Direct.
			case FileType.Type.GIF:  // Img#, Direct.
				/// 尝试利用 ImageSharp 压缩
				try {
					byte[] outbuffer = CompressTarget.GetImageSharpData(ref inbuffer, m_param);
					imageData = ImageDataFactory.Create(outbuffer);
					imageData.SetDpi(4096, 4096);
				}
				catch (Exception ex) {
					Logger.Log($"[ImageSharp Exception]: {ex.Message}.");
					imageData = null;
				}
				if (type == FileType.Type.WEBP)
					break;
				/// 尝试 直接加载
				try {
					imageData = CreateWithCheckingResize(ref inbuffer);
				}
				catch (Exception ex) {
					Logger.Log($"[iText Exception]: {ex.Message}.");
					imageData = null;
				}
				break;
			default:
				imageData = null;
				break;
			}
			return imageData;
		}

		/// <summary>
		/// 直接读取时（不全部压缩）的加载逻辑。
		/// </summary>
		/// <param name="inFile">欲加载之文件</param>
		/// <param name="compt">压缩器</param>
		/// <returns>加载出的数据，或者 null 若无法加载</returns>
		protected ImageData? LoadImageInMemory_Direct(FileType.Type type, ref byte[] inbuffer) {
			ImageData? imageData;
			switch (type) {
			case FileType.Type.JPEG: // CSI, Img#, Direct.
			case FileType.Type.PNG:  // CSI, Img#, Direct.
			case FileType.Type.TIFF: // CSI, Img#, Direct.
			case FileType.Type.BMP:  // Img#, Direct.
			case FileType.Type.GIF:  // Img#, Direct.
				/// 尝试 直接加载
				try {
					imageData = CreateWithCheckingResize(ref inbuffer);
				}
				catch (Exception ex) {
					Logger.Log($"[iText Exception]: {ex.Message}.");
					goto case FileType.Type.WEBP;
				}
				break;
			case FileType.Type.WEBP: // CSI, Img#.
				/// 尝试利用 Caesium-Iodine 压缩
				try {
					byte[] outbuffer = CompressTarget.GetCompressedImageData(ref inbuffer, m_param);
					imageData = ImageDataFactory.Create(outbuffer);
					imageData.SetDpi(4096, 4096);
				}
				catch (Exception ex) {
					Logger.Log($"[Iodine Exception]: {ex.Message}.");
				}
				/// 尝试利用 ImageSharp 压缩
				try {
					byte[] outbuffer = CompressTarget.GetImageSharpData(ref inbuffer, m_param);
					imageData = ImageDataFactory.Create(outbuffer);
					imageData.SetDpi(4096, 4096);
				}
				catch (Exception ex) {
					Logger.Log($"[ImageSharp Exception]: {ex.Message}.");
					imageData = null;
				}
				break;
			default:
				imageData = null;
				break;
			}
			return imageData;
		}

		private ImageData CreateWithCheckingResize(ref byte[] inbuffer) {
			var image = ImageDataFactory.Create(inbuffer);
			image.SetDpi(4096, 4096);

			if (m_param.resize && (m_param.width > 0 || m_param.height > 0 || m_param.shortSide > 0 || m_param.longSide > 0)) {
				(float width, float height) = CompressTarget.ComputeDimensionF(
					image.GetWidth(), image.GetHeight(),
					m_param.width, m_param.height,
					m_param.shortSide, m_param.longSide,
					m_param.reduceByPowOf2
				);
				if (!(width > image.GetWidth() || height > image.GetHeight())) {
					float xx = image.GetWidth() / width;
					float yy = image.GetHeight() / height;
					image.SetDpi((int)float.Round(4096 * xx), (int)float.Round(4096 * yy));
				}
			}

			return image;
		}
	}
}
