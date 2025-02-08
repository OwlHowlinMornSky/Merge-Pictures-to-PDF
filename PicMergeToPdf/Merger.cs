using iText.IO.Image;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using System.Buffers;
using System.IO.MemoryMappedFiles;
using static PicMerge.IMerger;

namespace PicMerge {
	internal class Merger(ImageParam ip) {

		protected string StrUnsupported = "Unsupported type.";
		protected string StrFailedToAdd = "Failed to add into pdf.";

		protected readonly ImageParam m_param = ip;

		/// <summary>
		/// 用于从文件加载图片。
		/// </summary>
		/// <param name="filepath">图片文件路径</param>
		/// <param name="compt">压缩器</param>
		/// <returns>加载结果</returns>
		internal ImageData? LoadImage(string filepath) {
			using FileStream inputStream = new(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
			return ReadImage(inputStream);
		}

		/// <summary>
		/// 从内存映射文件读取图片。
		/// </summary>
		/// <param name="inFile">要读入之图片</param>
		/// <param name="compt">压缩器</param>
		/// <returns>加载结果</returns>
		internal ImageData? ReadImage(Stream instream) {
			var type = FileType.CheckType(instream);
			byte[] inbuffer;
			if (instream.Length > int.MaxValue || instream.Length < 8) {
				return null;
			}
			inbuffer = ArrayPool<byte>.Shared.Rent((int)instream.Length);
			instream.Seek(0, SeekOrigin.Begin);
			instream.ReadExactly(inbuffer, 0, (int)instream.Length);
			return m_param.compress ? LoadImageInMemory_Compress(type, ref inbuffer) : LoadImageInMemory_Direct(type, ref inbuffer);
		}

		/// <summary>
		/// 压缩全部图片时的加载逻辑。
		/// </summary>
		/// <param name="inFile">欲加载之文件</param>
		/// <param name="compt">压缩器</param>
		/// <returns>加载出的数据，或者 null 若无法加载</returns>
		private ImageData? LoadImageInMemory_Compress(FileType.Type type, ref byte[] inbuffer) {
			ImageData? imageData = null;
			switch (type) {
			case FileType.Type.JPEG: // CSI, Img#, Direct.
			case FileType.Type.PNG:  // CSI, Img#, Direct.
			case FileType.Type.TIFF: // CSI, Img#, Direct.
			case FileType.Type.WEBP: // CSI, Img#.
				/// 尝试利用 Caesium-Iodine 压缩
				try {
					byte[] outbuffer = GetCompressedImageData(ref inbuffer);
					imageData = ImageDataFactory.Create(outbuffer);
				}
				catch (Exception) {
					imageData = null;
					goto case FileType.Type.GIF;
				}
				break;
			case FileType.Type.BMP:  // Img#, Direct.
			case FileType.Type.GIF:  // Img#, Direct.
				/// 尝试利用 ImageSharp 压缩
				try {
					using Image image = Image.Load(inbuffer);
					using MemoryStream imgSt = new();
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
					imageData = ImageDataFactory.Create(imgSt.ToArray());
					imgSt.Close();
				}
				catch (Exception) {
					imageData = null;
				}
				if (type == FileType.Type.WEBP)
					break;
				/// 尝试 直接加载
				try {
					imageData = ImageDataFactory.Create(inbuffer);
				}
				catch (Exception) {
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
		private ImageData? LoadImageInMemory_Direct(FileType.Type type, ref byte[] inbuffer) {
			ImageData? imageData = null;
			switch (type) {
			case FileType.Type.JPEG: // CSI, Img#, Direct.
			case FileType.Type.PNG:  // CSI, Img#, Direct.
			case FileType.Type.TIFF: // CSI, Img#, Direct.
			case FileType.Type.BMP:  // Img#, Direct.
			case FileType.Type.GIF:  // Img#, Direct.
				/// 尝试 直接加载
				try {
					imageData = ImageDataFactory.Create(inbuffer);
				}
				catch (Exception) {
					imageData = null;
					goto case FileType.Type.WEBP;
				}
				break;
			case FileType.Type.WEBP: // CSI, Img#.
				/// 尝试利用 Caesium-Iodine 压缩
				try {
					byte[] outbuffer = GetCompressedImageData(ref inbuffer);
					imageData = ImageDataFactory.Create(outbuffer);
				}
				catch (Exception) {
					imageData = null;
				}
				/// 尝试利用 ImageSharp 压缩
				try {
					using Image image = Image.Load(inbuffer);
					using MemoryStream imgSt = new();
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
					imageData = ImageDataFactory.Create(imgSt.ToArray());
					imgSt.Close();
				}
				catch (Exception) {
					imageData = null;
				}
				break;
			default:
				imageData = null;
				break;
			}
			return imageData;
		}

		private byte[] GetCompressedImageData(ref byte[] input) {
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
				ArrayPool<byte>.Shared.Return(tempOutBuffer);
			}
		}
	}
}
