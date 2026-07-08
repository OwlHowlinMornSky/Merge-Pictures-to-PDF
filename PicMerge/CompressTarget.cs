using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace PicMerge {
	internal static class CompressTarget {
		public static Stream GetCompressedImageData(in byte[] input, ImageParam param) {
			bool optimize = !param.compress; // 不压缩则使用优化
			var iodine_steam = PicCompress.BufferCompressor.Compress(
				input, param.format, param.quality, optimize,
				param.resize, param.width, param.height
			);
			return iodine_steam;
		}

		public static Stream GetImageSharpData(in Image input_image, ImageParam param) {
			bool optimize = !param.compress; // 不压缩则使用优化
			MemoryStream imgSt = new();

			if (param.resize) {
				input_image.Mutate(x => x.Resize(param.width, param.height));
			}

			switch (param.format) {
			case 1: { // JPEG
				JpegEncoder encoder = new() {
					SkipMetadata = true,
					ColorType = optimize ? JpegEncodingColor.YCbCrRatio444 : JpegEncodingColor.YCbCrRatio420,
					Quality = param.quality,
				};
				input_image.SaveAsJpeg(imgSt, encoder);
				break;
			}
			default:
			case 2: { // PNG
				if (optimize) {
					PngEncoder encoder = new() {
						SkipMetadata = true,
						ColorType = PngColorType.RgbWithAlpha,
						CompressionLevel = PngCompressionLevel.DefaultCompression,
						FilterMethod = PngFilterMethod.Adaptive,
						TransparentColorMode = PngTransparentColorMode.Preserve
					};
					input_image.SaveAsPng(imgSt, encoder);
				}
				else {
					Quantizer(in input_image, imgSt, param.quality);
				}
				break;
			}
			}

			return imgSt;
		}

		static void Quantizer(in Image image, MemoryStream output, int quality) {// 1. 根据 quality 值决定处理策略
			Image res;
			PngEncoder encoder;

			if (quality >= 90) {
				// 极高质量: 不量化，保留透明
				res = image;
				encoder = new PngEncoder {
					SkipMetadata = true,
					ColorType = PngColorType.RgbWithAlpha,
					TransparentColorMode = PngTransparentColorMode.Preserve,
					BitDepth = PngBitDepth.Bit8,
					CompressionLevel = PngCompressionLevel.BestCompression,
				};
			}
			else if (quality >= 70) {
				// 高质量: 不量化，保留透明
				res = image;
				encoder = new PngEncoder {
					SkipMetadata = true,
					ColorType = PngColorType.RgbWithAlpha,
					TransparentColorMode = PngTransparentColorMode.Preserve,
					BitDepth = PngBitDepth.Bit8,
					CompressionLevel = PngCompressionLevel.BestCompression,
					FilterMethod = PngFilterMethod.Adaptive,
				};
			}
			else if (quality >= 50) {
				// 较高质量: 轻微量化，保留透明
				res = image.Clone(ctx => ctx.Quantize(new WuQuantizer(new QuantizerOptions {
					Dither = OrderedDither.Bayer16x16,
					ColorMatchingMode = ColorMatchingMode.Hybrid,
					MaxColors = 256 // 将颜色减少到最多256色
				})));
				encoder = new PngEncoder {
					SkipMetadata = true,
					ColorType = PngColorType.Palette,
					TransparentColorMode = PngTransparentColorMode.Preserve,
					BitDepth = PngBitDepth.Bit8, // 调色板索引用8位
					CompressionLevel = PngCompressionLevel.BestCompression,
					FilterMethod = PngFilterMethod.Adaptive,
				};
			}
			else if (quality >= 30) {
				// 中等质量: 进一步减少颜色,移除透明
				res = image.Clone(ctx => ctx.Quantize(new WuQuantizer(new QuantizerOptions {
					Dither = OrderedDither.Bayer8x8,
					ColorMatchingMode = ColorMatchingMode.Hybrid,
					MaxColors = 256
				})));
				encoder = new PngEncoder {
					SkipMetadata = true,
					ColorType = PngColorType.Palette,
					TransparentColorMode = PngTransparentColorMode.Clear,
					BitDepth = PngBitDepth.Bit8, // 调色板索引用8位
					CompressionLevel = PngCompressionLevel.BestCompression,
					FilterMethod = PngFilterMethod.Adaptive,
				};
			}
			else {
				// 低质量: 强力压缩，降低位深
				res = image.Clone(ctx => ctx.Quantize(new WuQuantizer(new QuantizerOptions {
					Dither = OrderedDither.Bayer4x4,
					ColorMatchingMode = ColorMatchingMode.Coarse,
					MaxColors = 16
				})));
				encoder = new PngEncoder {
					SkipMetadata = true,
					ColorType = PngColorType.Palette,
					TransparentColorMode = PngTransparentColorMode.Clear,
					BitDepth = PngBitDepth.Bit4, // 调色板索引用4位，最多16色
					CompressionLevel = PngCompressionLevel.BestCompression,
					FilterMethod = PngFilterMethod.Adaptive,
				};
			}

			res.SaveAsPng(output, encoder);
		}
	}
}
