using iText.IO.Image;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using System.IO.MemoryMappedFiles;
using static PicMerge.IMerger;

namespace PicMerge {
	internal class Merger(Parameters param) {
		/// <summary>
		/// 合并之参数。使用第一张图片的尺寸时需要修改，所以不能只读。
		/// </summary>
		protected Parameters m_param = param;

		/// <summary>
		/// 用于文件并行方法之加载。
		/// </summary>
		/// <param name="filepath">图片文件路径</param>
		/// <returns>加载结果</returns>
		internal ImageData? ParaImage(string filepath) {
			using CompressTarget compt = new();
			return LoadImage(filepath, compt);
		}

		/// <summary>
		/// 用于从文件加载图片。
		/// </summary>
		/// <param name="filepath">图片文件路径</param>
		/// <param name="compt">压缩器</param>
		/// <returns>加载结果</returns>
		internal ImageData? LoadImage(string filepath, CompressTarget compt) {
			using FileStream inputStream = new(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using MemoryMappedFile inFile = MemoryMappedFile.CreateFromFile(inputStream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
			return ReadImage(inFile, compt);
		}

		/// <summary>
		/// 从内存映射文件读取图片。
		/// </summary>
		/// <param name="inFile">要读入之图片</param>
		/// <param name="compt">压缩器</param>
		/// <returns>加载结果</returns>
		internal ImageData? ReadImage(MemoryMappedFile inFile, CompressTarget compt) {
			return m_param.compress ? LoadImageInMemory_Compress(inFile, compt) : LoadImageInMemory_Direct(inFile, compt);
		}

		/// <summary>
		/// 压缩全部图片时的加载逻辑。
		/// </summary>
		/// <param name="inFile">欲加载之文件</param>
		/// <param name="compt">压缩器</param>
		/// <returns>加载出的数据，或者 null 若无法加载</returns>
		private ImageData? LoadImageInMemory_Compress(MemoryMappedFile inFile, CompressTarget compt) {
			ImageData? imageData = null;
			using var instream = inFile.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);

			var type = FileType.CheckType(instream);

			switch (type) {
			case FileType.Type.JPEG: // CSI, Img#, Direct.
			case FileType.Type.PNG:  // CSI, Img#, Direct.
			case FileType.Type.TIFF: // CSI, Img#, Direct.
			case FileType.Type.WEBP: // CSI, Img#.
				/// 尝试利用 Caesium-Iodine 压缩
				try {
					instream.Seek(0, SeekOrigin.Begin);
					int len = compt.Compressor.CompressFrom(
						inFile.SafeMemoryMappedFileHandle.DangerousGetHandle(), instream.Length,
						m_param.compressType, m_param.compressQuality
					);
					using var mapstream = compt.ViewStream;
					using BinaryReader br = new(mapstream);
					imageData = ImageDataFactory.Create(br.ReadBytes(len));
					br.Close();
					mapstream.Close();
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
					instream.Seek(0, SeekOrigin.Begin);
					using Image image = Image.Load(instream);
					using MemoryStream imgSt = new();
					switch (m_param.compressType) {
					case 2: {
						int quality = 10 - m_param.compressQuality / 10;
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
							Quality = m_param.compressQuality,
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
					instream.Seek(0, SeekOrigin.Begin);
					using BinaryReader br = new(instream);
					imageData = ImageDataFactory.Create(br.ReadBytes((int)instream.Length));
				}
				catch (Exception) {
					imageData = null;
				}
				break;
			case FileType.Type.ZIP:  // Archive.
			case FileType.Type._7ZIP:// Archive.
			case FileType.Type.RAR:  // Archive.
				throw new FileType.ArchiveException();
			default:
				throw new NotImplementedException("Unsupported type.");
			}
			return imageData;
		}

		/// <summary>
		/// 直接读取时（不全部压缩）的加载逻辑。
		/// </summary>
		/// <param name="inFile">欲加载之文件</param>
		/// <param name="compt">压缩器</param>
		/// <returns>加载出的数据，或者 null 若无法加载</returns>
		private ImageData? LoadImageInMemory_Direct(MemoryMappedFile inFile, CompressTarget compt) {
			ImageData? imageData = null;

			using var instream = inFile.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);

			var type = FileType.CheckType(instream);

			switch (type) {
			case FileType.Type.JPEG: // CSI, Img#, Direct.
			case FileType.Type.PNG:  // CSI, Img#, Direct.
			case FileType.Type.TIFF: // CSI, Img#, Direct.
			case FileType.Type.BMP:  // Img#, Direct.
			case FileType.Type.GIF:  // Img#, Direct.
				/// 尝试 直接加载
				try {
					instream.Seek(0, SeekOrigin.Begin);
					using BinaryReader br = new(instream);
					imageData = ImageDataFactory.Create(br.ReadBytes((int)instream.Length));
				}
				catch (Exception) {
					imageData = null;
					goto case FileType.Type.WEBP;
				}
				break;
			case FileType.Type.WEBP: // CSI, Img#.
				/// 尝试利用 Caesium-Iodine 压缩
				try {
					instream.Seek(0, SeekOrigin.Begin);
					int len = compt.Compressor.CompressFrom(
						inFile.SafeMemoryMappedFileHandle.DangerousGetHandle(), instream.Length,
						m_param.compressType, m_param.compressQuality
					);
					using var mapstream = compt.ViewStream;
					using BinaryReader br = new(mapstream);
					imageData = ImageDataFactory.Create(br.ReadBytes(len));
					br.Close();
					mapstream.Close();
				}
				catch (Exception) {
					imageData = null;
				}
				/// 尝试利用 ImageSharp 压缩
				try {
					instream.Seek(0, SeekOrigin.Begin);
					using Image image = Image.Load(instream);
					using MemoryStream imgSt = new();
					switch (m_param.compressType) {
					case 2: {
						int quality = 10 - m_param.compressQuality / 10;
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
							Quality = m_param.compressQuality,
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
			case FileType.Type.ZIP:  // Archive.
			case FileType.Type._7ZIP:// Archive.
			case FileType.Type.RAR:  // Archive.
				throw new FileType.ArchiveException();
			default:
				throw new NotImplementedException("Unsupported type.");
			}
			return imageData;
		}
	}
}
