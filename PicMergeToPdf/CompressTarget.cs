using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using System.Buffers;
using static PicMerge.IMerger;

namespace PicMerge {
	internal static class CompressTarget {
		public static byte[] GetCompressedImageData(ref byte[] input, ImageParam m_param) {
			byte[] tempOutBuffer = ArrayPool<byte>.Shared.Rent(MapFileSize);
			try {
				int len = PicCompress.BufferCompressor.Compress(
					ref input, ref tempOutBuffer,
					m_param.format, m_param.quality,
					m_param.resize, m_param.width, m_param.height, m_param.shortSide, m_param.longSide,
					m_param.reduceByPowOf2
				);
				ReadOnlySpan<byte> tempSpan = new(tempOutBuffer, 0, len);
				return tempSpan.ToArray();
			}
			finally {
				ArrayPool<byte>.Shared.Return(tempOutBuffer, true);
			}
		}

		public static byte[] GetImageSharpData(ref byte[] input, ImageParam m_param) {
			using Image image = Image.Load(input);
			using MemoryStream imgSt = new();

			if (m_param.resize && (m_param.width > 0 || m_param.height > 0 || m_param.shortSide > 0 || m_param.longSide > 0)) {
				(int width, int height) = ComputeDimension(
					image.Width, image.Height,
					m_param.width, m_param.height,
					m_param.shortSide, m_param.longSide,
					m_param.reduceByPowOf2
				);
				if (!(width > image.Width || height > image.Height)) {
					image.Mutate(x => x.Resize(width, height));
				}
			}

			switch (m_param.format) {
			case 2: {
				int quality = 10 - m_param.quality / 10;
				PngEncoder encoder = new() {
					SkipMetadata = true,
					ColorType = PngColorType.Rgb,
					CompressionLevel = quality switch {
						1 => PngCompressionLevel.Level1,
						2 => PngCompressionLevel.Level2,
						3 => PngCompressionLevel.Level3,
						4 => PngCompressionLevel.Level4,
						5 => PngCompressionLevel.Level5,
						6 => PngCompressionLevel.Level6,
						7 => PngCompressionLevel.Level7,
						8 => PngCompressionLevel.Level8,
						9 => PngCompressionLevel.Level9,
						10 => PngCompressionLevel.Level9,
						_ => PngCompressionLevel.Level0,
					}
				};
				image.SaveAsPng(imgSt, encoder);
				break;
			}
			default: {
				JpegEncoder encoder = new() {
					SkipMetadata = true,
					ColorType = JpegEncodingColor.Rgb,
					Quality = m_param.quality,
					Interleaved = false
				};
				image.SaveAsJpeg(imgSt, encoder);
				break;
			}
			}
			return imgSt.ToArray();
		}

		public static (int, int) ComputeDimension(
			int original_width,
			int original_height,
			int desired_width,
			int desired_height,
			int short_side_pixels,
			int long_size_pixels,
			bool rbpo2
		) {
			if (desired_width == 0 && desired_height == 0 && (short_side_pixels != 0 || long_size_pixels != 0)) {
				if (original_width < original_height) {
					desired_width = short_side_pixels;
					desired_height = long_size_pixels;
				}
				else {
					desired_height = short_side_pixels;
					desired_width = long_size_pixels;
				}
			}

			float n_width = original_width;
			float n_height = original_height;
			float ratio = 1.0f * original_width / original_height;

			if (rbpo2 && ((desired_width > 3 && desired_width < original_width) || (desired_height > 3 && desired_height < original_height))) {
				float dw = desired_width;
				float dh = desired_height;
				while ((desired_width > 3 && n_width > dw) || (desired_height > 3 && n_height > dh)) {
					n_width /= 2.0f;
					n_height /= 2.0f;
				}
				n_width = float.Ceiling(n_width);
				n_height = float.Ceiling(n_height);
			}
			else {
				n_width = desired_width;
				n_height = desired_height;

				if (desired_width > 0 && desired_height > 0) {
					return (desired_width, desired_height);
				}

				if (desired_height == 0) {
					n_height = float.Round(n_width / ratio);
				}

				if (desired_width == 0) {
					n_width = float.Round(n_height * ratio);
				}
			}
			return ((int)n_width, (int)n_height);
		}

		public static (float, float) ComputeDimensionF(
			float original_width,
			float original_height,
			int desired_width,
			int desired_height,
			int short_side_pixels,
			int long_size_pixels,
			bool rbpo2
		) {
			if (desired_width == 0 && desired_height == 0 && (short_side_pixels != 0 || long_size_pixels != 0)) {
				if (original_width < original_height) {
					desired_width = short_side_pixels;
					desired_height = long_size_pixels;
				}
				else {
					desired_height = short_side_pixels;
					desired_width = long_size_pixels;
				}
			}

			float n_width = original_width;
			float n_height = original_height;
			float ratio = original_width / original_height;

			if (rbpo2 && ((desired_width > 3 && desired_width < original_width) || (desired_height > 3 && desired_height < original_height))) {
				float dw = desired_width;
				float dh = desired_height;
				while ((desired_width > 3 && n_width > dw) || (desired_height > 3 && n_height > dh)) {
					n_width /= 2.0f;
					n_height /= 2.0f;
				}
			}
			else {
				n_width = desired_width;
				n_height = desired_height;

				if (desired_width > 0 && desired_height > 0) {
					return (desired_width, desired_height);
				}

				if (desired_height == 0) {
					n_height = n_width / ratio;
				}

				if (desired_width == 0) {
					n_width = n_height * ratio;
				}
			}
			return (n_width, n_height);
		}
	}
}
