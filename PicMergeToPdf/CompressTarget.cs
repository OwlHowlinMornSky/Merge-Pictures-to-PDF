using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace PicMerge {
	internal static class CompressTarget {
		public static Stream GetCompressedImageData(in byte[] input, ImageParam param, bool opimize) {
			var iodine_steam = PicCompress.BufferCompressor.Compress(
				input, param.format, param.quality, opimize,
				param.resize, param.width, param.height
			);
			return iodine_steam;
		}

		public static Stream GetImageSharpData(in Image input_image, ImageParam param) {
			MemoryStream imgSt = new();

			if (param.resize) {
				input_image.Mutate(x => x.Resize(param.width, param.height));
			}

			switch (param.format) {
			case 2: { // PNG
				int quality = 10 - param.quality / 10;
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
				input_image.SaveAsPng(imgSt, encoder);
				break;
			}
			//case 1:// JPEG
			default: { // others
				JpegEncoder encoder = new() {
					SkipMetadata = true,
					ColorType = JpegEncodingColor.Rgb,
					Quality = param.quality,
					Interleaved = false
				};
				input_image.SaveAsJpeg(imgSt, encoder);
				break;
			}
			}

			return imgSt;
		}
	}
}
