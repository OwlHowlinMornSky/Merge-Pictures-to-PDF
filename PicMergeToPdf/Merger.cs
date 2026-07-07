using SixLabors.ImageSharp;

namespace PicMerge {
	internal class Merger() {

		protected string StrFailedToRead = "Failed to process.";
		protected string StrFailedToAdd = "Failed to add into PDF.";

		protected readonly object m_lock = new(); // Used to avoid IO at the same time.

		internal struct LoadImageLog(string msg) {
			public string error_message = msg;

			public void operator +=(string msg) {
				error_message += Environment.NewLine;
				error_message += msg;
			}
		}

		internal struct LoadImageResult(string msg) {
			public Stream? output = null;

			public LoadImageLog log = new(msg);

			public int img_w_override = -1;
			public int img_h_override = -1;
		}

		/// <summary>
		/// 用于从文件加载图片。
		/// </summary>
		/// <param name="filepath">图片文件路径</param>
		/// <returns>加载结果</returns>
		internal LoadImageResult LoadImage(string filepath, ImageParam param) {
			LoadImageResult res = new($"[Load] Loading \'{filepath}\'...");
			try {
				byte[] buffer;
				lock (m_lock) { // 防止同时IO
					using FileStream inputStream = new(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
					inputStream.Seek(0, SeekOrigin.Begin);
					buffer = new byte[inputStream.Length];
					inputStream.ReadExactly(buffer, 0, (int)inputStream.Length);
					inputStream.Close();
				}
				res.output = ReadImage(in buffer, param, ref res.log, out res.img_w_override, out res.img_h_override);
			}
			catch (Exception ex) {
				res.log += $"[Load] Exception: {ex.Message}";
				res.output = null;
			}
			if (res.output is null) {
				res.log += $"[Load] Loading '{filepath}' Failed.";
			}
			else {
				res.log += $"[Load] '{filepath}' Loaded.";
			}
			return res;
		}

		/// <summary>
		/// 从内存映射文件读取图片。
		/// </summary>
		/// <param name="input">要读入之图片之文件流</param>
		/// <returns>加载结果</returns>
		internal static Stream? ReadImage(in byte[] buffer, ImageParam param, ref LoadImageLog log, out int img_w_or, out int img_h_or) {
			Stream? img_stream = null;
			img_w_or = -1;
			img_h_or = -1;
			try {
				FileType.Type type;
				type = FileType.CheckType(buffer);

				log += $"[Read] Image format: '{type}'.";

				if (type == FileType.Type.Unknown)
					return null;

				log += $"[Read] Checking in... (invalid file if no 'checked in' followed)";
				using Image image = Image.Load(buffer);
				log += $"[Read] Checked in.";

				/// 计算尺寸
				param = ProcessExtra(image.Width, image.Height, param);
#if DEBUG
				log += $"[Read] Computed size: {param.width}, {param.height}";
				if (param.reduceByPowOf2 || param.shortSide != 0 || param.longSide != 0) {
					throw new ArgumentException("Extra parameters not processed.");
				}
				if (param.resize && (param.width < 1 || param.height < 1)) {
					throw new ArgumentException("Parameters is invalid.");
				}
#endif
				bool optimize = !param.compress; // 不压缩则使用优化
				if (optimize) // 优化则使质量最高，保证不接收optimize的格式也能接近无损
					param.quality = 100;

				if (param.compress || param.format != 0 || param.resize) {
					// 压缩、改变格式和改变大小都需要尝试处理：
					// 先尝试Iodine，最大化压缩率；
					// 再尝试ImageSharp，转换的同时可以压缩；
					// 最后尝试直接加载。
					img_stream ??= LoadImgaeByIodine(type, in buffer, param, optimize, ref log);
					img_stream ??= LoadImageByImageSharp(type, in image, param, ref log);
					img_stream ??= LoadImageDirectly(type, in buffer, param, ref log, out img_w_or, out img_h_or);
				}
				else {
					// 既不压缩、也不改变大小时：
					// 先尝试直接加载，忽略计算的图片尺寸；
					// 再尝试ImageSharp，可读取的格式更多；
					// 最后尝试Iodine，不能闲着。（www
					img_stream ??= LoadImageDirectly(type, in buffer, param, ref log, out _, out _);
					img_stream ??= LoadImageByImageSharp(type, in image, param, ref log);
					img_stream ??= LoadImgaeByIodine(type, in buffer, param, optimize, ref log);
				}

				if (img_stream is null) {
					log += $"[Read] Reading Failed, no any tool supports the format.";
				}
			}
			catch (Exception ex) {
				log += $"[Read] Exception: {ex.Message}";
				img_stream = null;
			}
			return img_stream;
		}

		protected static Stream? LoadImgaeByIodine(FileType.Type type, in byte[] buffer, ImageParam param, bool optimize, ref LoadImageLog log) {
			Stream? imageData = null;
			switch (type) {
			case FileType.Type.WEBP: // Iodine, Img#.
			case FileType.Type.GIF:  // Iodine, Img#.
			case FileType.Type.TIFF: // Iodine, Img#.
				switch (param.format) { // 必须转换
				case 1: // JPEG
				case 2: // PNG
					break;
				//case 0: // 原格式
				default:
					param.format = 2; // To PNG
					break;
				}
				goto case FileType.Type.JPEG;
			case FileType.Type.JPEG: // Iodine, Img#, Direct.
			case FileType.Type.PNG:  // Iodine, Img#, Direct.
				try {
					imageData = CompressTarget.GetCompressedImageData(in buffer, param, optimize);
				}
				catch (Exception ex) {
					log += $"[Iodine] Exception: {ex.Message}";
					imageData = null;
				}
				break;
			case FileType.Type.BMP:  // Img#.
			default:
				break;
			}
			if (imageData is null) {
				log += $"[Iodine] Cannot do it. Type: '{type}'.";
			}
			return imageData;
		}

		protected static Stream? LoadImageByImageSharp(FileType.Type type, in Image image, ImageParam param, ref LoadImageLog log) {
			Stream? imageData = null;
			switch (type) {
			case FileType.Type.GIF:  // Iodine, Img#.
			case FileType.Type.TIFF: // Iodine, Img#.
			case FileType.Type.WEBP: // Iodine, Img#.
			case FileType.Type.BMP:  // Img#.
			/*switch (param.format) { // 必须转换
			case 1: // JPEG
			case 2: // PNG
				break;
			//case 0: // 原格式
			default:
				param.format = 2; // To PNG
				break;
			} // ImageSharp目前只会输出PNG和Jpeg.
			goto case FileType.Type.JPEG;*/
			case FileType.Type.JPEG: // Iodine, Img#, Direct.
			case FileType.Type.PNG:  // Iodine, Img#, Direct.
				try {
					imageData = CompressTarget.GetImageSharpData(in image, param);
				}
				catch (Exception ex) {
					log += $"[ImageSharp] Exception: {ex.Message}";
					imageData = null;
				}
				break;
			default:
				break;
			}
			if (imageData is null) {
				log += $"[ImageSharp] Cannot do it. Type: '{type}'.";
			}
			return imageData;
		}

		private static MemoryStream? LoadImageDirectly(FileType.Type type, in byte[] buffer, ImageParam param, ref LoadImageLog log, out int img_w, out int img_h) {
			if (param.resize) {
				img_w = param.width;
				img_h = param.height;
			}
			else {
				img_w = -1;
				img_h = -1;
			}
			switch (type) {
			case FileType.Type.JPEG: // Iodine, Img#, Direct.
			case FileType.Type.PNG:  // Iodine, Img#, Direct.
				break;
			case FileType.Type.GIF:  // Iodine, Img#.
			case FileType.Type.TIFF: // Iodine, Img#.
			case FileType.Type.WEBP: // Iodine, Img#.
			case FileType.Type.BMP:  // Img#.
			default:
				log += $"[PDFsharp] Not supports '{type}'.";
				return null;
			}
			MemoryStream? res;
			try {
				res = new MemoryStream(buffer, false);
			}
			catch (Exception ex) {
				log += $"[PDFsharp] Exception: {ex.Message}";
				res = null;
			}
			if (res is null) {
				log += $"[PDFsharp] Cannot do it. Type: '{type}'.";
			}
			return res;
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
