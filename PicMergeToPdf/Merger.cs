using iText.IO.Image;
using SixLabors.ImageSharp;

namespace PicMerge {
	internal class Merger() {

		protected string StrFailedToRead = "Failed to process.";
		protected string StrFailedToAdd = "Failed to add into PDF.";

		protected readonly object m_lock = new(); // Used to avoid IO at the same time.

		internal struct LoadImageLog() {
			public string error_message = "";

			public void operator +=(string msg) {
				error_message += Environment.NewLine;
				error_message += msg;
			}
		}

		/// <summary>
		/// 用于从文件加载图片。
		/// </summary>
		/// <param name="filepath">图片文件路径</param>
		/// <returns>加载结果</returns>
		internal ImageData? LoadImage(string filepath, ImageParam param, ref LoadImageLog log) {
			log.error_message = $"[Load] Loading \'{filepath}\'...";
			ImageData? res;
			try {
				using FileStream inputStream = new(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
				res = ReadImage(inputStream, param, ref log);
			}
			catch (Exception ex) {
				res = null;
				log += $"[Load] Exception: {ex.Message} by {ex.Source}.";
			}
			if (res == null) {
				log += $"[Load] Failed loading \'{filepath}\'.";
			}
			else {
				log += $"[Load] Loaded \'{filepath}\'.";
			}
			return res;
		}

		/// <summary>
		/// 从内存映射文件读取图片。
		/// </summary>
		/// <param name="instream">要读入之图片之文件流</param>
		/// <returns>加载结果</returns>
		internal ImageData? ReadImage(Stream instream, ImageParam param, ref LoadImageLog log) {
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
				log += $"[Read] Image format: {type}.";

				return param.compress ? LoadImageInMemory_Compress(type, in inbuffer, param, ref log) : LoadImageInMemory_Direct(type, in inbuffer, param, ref log);
			}
			catch (Exception ex) {
				log += $"[Read] Exception: {ex.Message} by {ex.Source}.";
				return null;
			}
		}

		/// <summary>
		/// 尝试压缩全部图片时的加载逻辑。
		/// </summary>
		/// <returns>加载出的数据，或者 null 若无法加载</returns>
		protected ImageData? LoadImageInMemory_Compress(FileType.Type type, in byte[] inbuffer, ImageParam param, ref LoadImageLog log) {
			log += $"[Read] Reading... (Invalid image if failed)";
			using Image image = Image.Load(inbuffer);
			log += $"[Read] Finished. Valid image.";

			/// 计算尺寸
			param = ProcessExtra(image.Width, image.Height, param);
#if DEBUG
			log += $"[Read] Computed size: {param.width}, {param.height}";
#endif

			ImageData? imageData = null;
			switch (type) {
			case FileType.Type.JPEG: // Iodine, Img#, Direct.
			case FileType.Type.PNG:  // Iodine, Img#, Direct.
			case FileType.Type.GIF:  // Iodine, Img#, Direct.
			case FileType.Type.TIFF: // Iodine, Img#, Direct.
				/// 尝试利用 Caesium-Iodine 压缩
				try {
					byte[] outbuffer = CompressTarget.GetCompressedImageData(in inbuffer, param);
					imageData = ImageDataFactory.Create(outbuffer);
					imageData.SetDpi(
						PdfTarget.BaseDpiForDirectLoadComputingSize,
						PdfTarget.BaseDpiForDirectLoadComputingSize
					);
				}
				catch (Exception ex) {
					log += $"[Iodine] Exception: {ex.Message} by {ex.Source}.";
					imageData = null;
				}
				if (imageData == null)
					goto case FileType.Type.BMP;
				break;
			case FileType.Type.WEBP: // Iodine, Img#.
				/// 尝试利用 Caesium-Iodine 压缩
				try {
					var ppp = param;
					ppp.format = 1; // WEBP必须转换
					byte[] outbuffer = CompressTarget.GetCompressedImageData(in inbuffer, ppp);
					imageData = ImageDataFactory.Create(outbuffer);
					imageData.SetDpi(
						PdfTarget.BaseDpiForDirectLoadComputingSize,
						PdfTarget.BaseDpiForDirectLoadComputingSize
					);
				}
				catch (Exception ex) {
					log += $"[Iodine] Exception: {ex.Message} by {ex.Source}.";
					imageData = null;
				}
				if (imageData == null)
					goto case FileType.Type.BMP;
				break;
			case FileType.Type.BMP:  // Img#, Direct.
				/// 尝试利用 ImageSharp 压缩
				try {
					byte[] outbuffer = CompressTarget.GetImageSharpData(in image, param);
					imageData = ImageDataFactory.Create(outbuffer);
					imageData.SetDpi(
						PdfTarget.BaseDpiForDirectLoadComputingSize,
						PdfTarget.BaseDpiForDirectLoadComputingSize
					);
				}
				catch (Exception ex) {
					log += $"[ImageSharp] Exception: {ex.Message} by {ex.Source}.";
					imageData = null;
				}
				if (imageData == null)
					goto default;
				break;
			default:
				/// 尝试 直接加载
				try {
					imageData = CreateWithCheckingResize(in inbuffer, param);
				}
				catch (Exception ex) {
					log += $"[iText] Exception: {ex.Message} by {ex.Source}.";
					imageData = null;
				}
				break;
			}
			return imageData;
		}

		/// <summary>
		/// 直接读取时（不全部压缩）的加载逻辑。
		/// </summary>
		/// <returns>加载出的数据，或者 null 若无法加载</returns>
		protected ImageData? LoadImageInMemory_Direct(FileType.Type type, in byte[] inbuffer, ImageParam param, ref LoadImageLog log) {
			Image? image = null;
			try {
				image = Image.Load(inbuffer);
			}
			catch {
				log.error_message += Environment.NewLine;
				log.error_message += $"[Read] Error: Invalid image file.";
				return null;
			}

			/// 计算尺寸
			param = ProcessExtra(image.Width, image.Height, param);
#if DEBUG
			log += $"[Read] Computed size: {param.width}, {param.height}";
#endif
			param.quality = 100; // 不压缩

			ImageData? imageData = null;
			switch (type) {
			case FileType.Type.JPEG: // Iodine, Img#, Direct.
			case FileType.Type.PNG:  // Iodine, Img#, Direct.
			case FileType.Type.GIF:  // Iodine, Img#, Direct.
			case FileType.Type.TIFF: // Iodine, Img#, Direct.
			case FileType.Type.BMP:  // Img#, Direct.
				/// 尝试 直接加载
				try {
					imageData = CreateWithCheckingResize(in inbuffer, param);
				}
				catch (Exception ex) {
					log += $"[iText] Exception: {ex.Message} by {ex.Source}.";
					imageData = null;
					goto case FileType.Type.WEBP;
				}
				break;
			case FileType.Type.WEBP: // Iodine, Img#.
				/// 尝试利用 ImageSharp 转化
				try {
					byte[] outbuffer = CompressTarget.GetImageSharpData(in image, param);
					imageData = ImageDataFactory.Create(outbuffer);
					imageData.SetDpi(
						PdfTarget.BaseDpiForDirectLoadComputingSize,
						PdfTarget.BaseDpiForDirectLoadComputingSize
					);
				}
				catch (Exception ex) {
					log += $"[ImageSharp] Exception: {ex.Message} by {ex.Source}.";
					imageData = null;
				}
				if (type == FileType.Type.BMP)
					break;
				if (imageData == null)
					goto default;
				break;
			default:
				/// 尝试利用 Iodine 转化
				try {
					var ppp = param;
					ppp.format = 1; // 最终保底：全部尝试转化为jpeg
					byte[] outbuffer = CompressTarget.GetCompressedImageData(in inbuffer, ppp);
					imageData = ImageDataFactory.Create(outbuffer);
					imageData.SetDpi(
						PdfTarget.BaseDpiForDirectLoadComputingSize,
						PdfTarget.BaseDpiForDirectLoadComputingSize
					);
				}
				catch (Exception ex) {
					log += $"[Iodine] Exception: {ex.Message} by {ex.Source}.";
					imageData = null;
				}
				break;
			}
			return imageData;
		}

		private ImageData CreateWithCheckingResize(in byte[] inbuffer, ImageParam param) {
#if DEBUG
			if (param.reduceByPowOf2 || param.shortSide != 0 || param.longSide != 0) {
				throw new ArgumentException("Extra parameters not processed.");
			}
			if (param.resize && (param.width < 1 || param.height < 1)) {
				throw new ArgumentException("Parameters is invalid.");
			}
#endif
			var image = ImageDataFactory.Create(inbuffer);
			image.SetDpi(
				PdfTarget.BaseDpiForDirectLoadComputingSize,
				PdfTarget.BaseDpiForDirectLoadComputingSize
			);

			if (param.resize) {
				float xx = image.GetWidth() / param.width;
				float yy = image.GetHeight() / param.height;
				image.SetDpi(
					(int)float.Round(PdfTarget.BaseDpiForDirectLoadComputingSize * xx),
					(int)float.Round(PdfTarget.BaseDpiForDirectLoadComputingSize * yy)
				);
			}

			return image;
		}

		static ImageParam ProcessExtra(int org_w, int org_h, ImageParam param) {
			(float w, float h) = ComputeDimensionShell(
				org_w, org_h,
				param.width, param.height,
				param.shortSide, param.longSide,
				param.reduceByPowOf2
			);

			var p = param;
			p.shortSide = 0; p.longSide = 0;
			p.reduceByPowOf2 = false;

			float ratio = w / h;

			bool allow_scale_up = false; // TO BE DONE.
			if (!allow_scale_up) {
				float wscale = w / org_w;
				float hscale = h / org_h;
				if (wscale > 1.0f || hscale > 1.0f) {
					// 谁倍数更大，按谁收缩
					if (wscale > hscale) {
						w = org_w;
						h = org_w / ratio;
					}
					else {
						h = org_h;
						w = org_h * ratio;
					}
				}
			}

			p.width = (int)float.Round(w);
			p.height = (int)float.Round(h);

			if (p.width > org_w || p.height > org_h) {
				p.width = org_w;
				p.height = org_h;
			}

			return p;
		}

		static (float, float) ComputeDimensionShell(
			int original_width, int original_height,
			int desired_width, int desired_height,
			int short_side_pixels, int long_size_pixels,
			bool shrink_by_2
		) {
			float whratio = original_width * 1.0f / original_height;

			(float reswidth, float resheight) = ComputeDimensionCore(
				original_width, original_height,
				desired_width, desired_height,
				short_side_pixels, long_size_pixels,
				shrink_by_2
			);

			// 如果结果有0，按原始长宽比推算
			if (reswidth == 0)
				return (resheight * whratio, resheight);
			if (resheight == 0)
				return (reswidth, reswidth / whratio);

			// 限制范围
			if (reswidth < 4)
				reswidth = 4;
			else if (reswidth > 16_777_216)
				reswidth = 16_777_216;

			if (resheight < 4)
				resheight = 4;
			else if (resheight > 16_777_216)
				resheight = 16_777_216;

			return (reswidth, resheight);
		}

		static (float, float) ComputeDimensionCore(
			int original_width, int original_height,
			int desired_width, int desired_height,
			int short_side_pixels, int long_size_pixels,
			bool shrink_by_2
		) {
			// 如果有直接设置尺寸的，忽略额外参数
			if (desired_width != 0 || desired_height != 0) {
				;
			}
			// 如果按长边或短边，则要比较
			else if (short_side_pixels != 0 || long_size_pixels != 0) {
				if (original_width < original_height) {
					desired_width = short_side_pixels;
					desired_height = long_size_pixels;
				}
				else {
					desired_height = short_side_pixels;
					desired_width = long_size_pixels;
				}
			}
			// 如果全为0，返回原始尺寸
			else {
				return (original_width, original_height);
			}

			// 如果无需按2的幂次缩小，可以返回
			if (!shrink_by_2) {
				return (desired_width, desired_height);
			}

			// 下面按2的幂次缩小
			float n_width = original_width;
			float n_height = original_height;

			// 不允许小于4
			if (desired_width < 4) {
				n_width = 0;
			}
			else {
				while (n_width > desired_width) {
					n_width /= 2.0f;
				}
			}

			if (desired_height < 4) {
				n_height = 0;
			}
			else {
				while (n_height > desired_height) {
					n_height /= 2.0f;
				}
			}

			return (n_width, n_height);
		}
	}
}
