using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf;
using SixLabors.ImageSharp;
using IOP = System.IO.Path;
using System.IO.MemoryMappedFiles;
using SixLabors.ImageSharp.Formats.Gif;

namespace PicMerge {
	public static class Main {

		public static Action SingleUpdate = () => { };

		private static readonly PicCompress.Compressor compressor = new();
		private const long MapFileSize = 0x04000000;

		public static List<string> Process(string outputfilepath, List<string> files, int pageSizeType, float pagesizex, float pagesizey, bool compress, string Title = "") {
			List<string> failed = [];

			Func<string, ImageData?> load = compress ? LoadImage_Compress : LoadImage_Direct;

			int i = 0;
			ImageData? imageData = null;
			for (; i < files.Count; ++i) {
				string file = files[i];
				imageData = load(file);
				if (imageData != null) {
					break;
				}
				failed.Add(file);
				SingleUpdate();
			}
			if (imageData == null) {
				return [];
			}

			using FileStream stream = new(outputfilepath, FileMode.CreateNew, FileAccess.Write);
			WriterProperties writerProperties = new();
			writerProperties.SetFullCompressionMode(true);
			writerProperties.SetCompressionLevel(CompressionConstants.BEST_COMPRESSION);

			using (PdfWriter writer = new(stream, writerProperties)) {
				using PdfDocument pdfDocument = new(writer);
				pdfDocument.GetDocumentInfo().SetKeywords(Title);

				AddImage(imageData, pdfDocument, pageSizeType, ref pagesizex, ref pagesizey);
				SingleUpdate();
				for (++i; i < files.Count; ++i) {
					string file = files[i];
					imageData = load(file);
					if (imageData == null) {
						failed.Add(file);
						SingleUpdate();
						continue;
					}
					AddImage(imageData, pdfDocument, pageSizeType, ref pagesizex, ref pagesizey);
					SingleUpdate();
				}
			}

			stream.Close();
			return failed;
		}

		private static ImageData? LoadImage_Compress(string file) {
			ImageData? imageData = null;

			// 尝试利用 Caesium-Iodine 压缩
			try {
				using var mapfile = MemoryMappedFile.CreateNew(null, MapFileSize);
				int len = compressor.Compress(file, mapfile.SafeMemoryMappedFileHandle.DangerousGetHandle(), MapFileSize);
				using var mapstream = mapfile.CreateViewStream();
				using BinaryReader br = new(mapstream);
				imageData = ImageDataFactory.Create(br.ReadBytes(len));
				br.Close();
				mapstream.Close();
			}
			catch (Exception) { }
			if (imageData != null)
				return imageData;

			// 若不支持则 尝试 直接加载
			try {
				imageData = ImageDataFactory.Create(file);
			}
			catch (Exception) { }
			if (imageData != null)
				return imageData;

			// 若不支持则 尝试利用 ImageSharp 压缩
			try {
				GifEncoder gifEncoder = new() {
					SkipMetadata = true
				};
				using Image image = Image.Load(file);
				using MemoryStream imgSt = new();
				image.SaveAsGif(imgSt, gifEncoder);
				imageData = ImageDataFactory.Create(imgSt.ToArray());
				imgSt.Close();
			}
			catch (Exception) { }

			return imageData;
		}

		private static ImageData? LoadImage_Direct(string file) {
			ImageData? imageData = null;

			// 尝试 直接加载
			try {
				imageData = ImageDataFactory.Create(file);
			}
			catch (Exception) { }
			if (imageData != null)
				return imageData;

			// 若不支持则 尝试利用 Caesium-Iodine 压缩
			try {
				using var mapfile = MemoryMappedFile.CreateNew(null, MapFileSize);
				int len = compressor.Compress(file, mapfile.SafeMemoryMappedFileHandle.DangerousGetHandle(), MapFileSize);
				using var mapstream = mapfile.CreateViewStream();
				using BinaryReader br = new(mapstream);
				imageData = ImageDataFactory.Create(br.ReadBytes(len));
				br.Close();
				mapstream.Close();
			}
			catch (Exception) { }
			if (imageData != null)
				return imageData;

			// 若不支持则 尝试利用 ImageSharp 压缩
			try {
				GifEncoder gifEncoder = new() {
					SkipMetadata = true
				};
				using Image image = Image.Load(file);
				using MemoryStream imgSt = new();
				image.SaveAsGif(imgSt, gifEncoder);
				imageData = ImageDataFactory.Create(imgSt.ToArray());
				imgSt.Close();
			}
			catch (Exception) { }

			return imageData;
		}

		private static void AddImage(ImageData imageData, PdfDocument pdfDocument, int pageSizeType, ref float pagesizex, ref float pagesizey) {
			PageSize pageSize;
			PageSize imageSize;
			float width = imageData.GetWidth();
			float height = imageData.GetHeight();
			switch (pageSizeType) {
			default:
			case 1:
				imageSize = new(width, height);
				pageSize = imageSize;
				break;
			case 2:
				if (pagesizex == 0) {
					pagesizex = width;
				}
				imageSize = new(pagesizex, pagesizex / width * height);
				pageSize = imageSize;
				break;
			case 3:
				if (pagesizex == 0 || pagesizey == 0) {
					pagesizex = width;
					pagesizey = height;
				}
				pageSize = new(pagesizex, pagesizey);
				float r = float.Min(
					1.0f * pagesizex / width,
					1.0f * pagesizey / height
				);
				imageSize = new(width * r, height * r);
				imageSize.SetX((pageSize.GetWidth() - imageSize.GetWidth()) / 2.0f);
				imageSize.SetY((pageSize.GetHeight() - imageSize.GetHeight()) / 2.0f);
				break;
			}
			pageSize.SetWidth(pageSize.GetWidth());
			pageSize.SetHeight(pageSize.GetHeight());
			PdfPage page = pdfDocument.AddNewPage(pageSize);
			PdfCanvas canvas = new(page, true);
			canvas.AddImageFittedIntoRectangle(imageData, imageSize, true);
		}
	}
}

