using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf;
using SixLabors.ImageSharp;
using IOP = System.IO.Path;
using System.IO.MemoryMappedFiles;

namespace PicMerge {
	public static class Main {

		public static Func<int> BeginSingle = () => 0;
		public static Action<int, int, int> SingleUpdate = (id, cnt, n) => { };

		private static readonly PicCompress.Compressor compressor = new();
		private const long MapFileSize = 0x04000000;

		public static List<string> Process(string outputfilepath, List<string> files, int pageSizeType, float pagesizex, float pagesizey, string Title = "") {
			int id = BeginSingle();

			List<string> failed = [];

			bool warningedFormat = false;

			/*SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder encoder = new() {
				SkipMetadata = true,
				Quality = 90,
				ColorType = SixLabors.ImageSharp.Formats.Jpeg.JpegEncodingColor.Rgb,
				Interleaved = false
			};
			*/

			/*SixLabors.ImageSharp.Formats.Png.PngEncoder encoder = new() {
				ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.Rgb,
				SkipMetadata = true,
				BitDepth = SixLabors.ImageSharp.Formats.Png.PngBitDepth.Bit8
			};*/


			SixLabors.ImageSharp.Formats.Gif.GifEncoder gifEncoder = new() {
				SkipMetadata = true
			};

			int i = 0;
			ImageData? imageData = null;
			for (; i < files.Count; ++i) {
				SingleUpdate(id, i, files.Count);
				string file = files[i];
				try {
					try { // 尝试直接加载
						imageData = ImageDataFactory.Create(file);
					}
					catch (Exception) { // 若不支持则转码
						using Image image = Image.Load(file);
						using MemoryStream imgSt = new();
						//image.SaveAsJpeg(imgSt, encoder);
						//image.SaveAsPng(imgSt, encoder);
						//image.SaveAsTiff(imgSt);
						//image.SaveAsBmp(imgSt);
						image.SaveAsGif(imgSt, gifEncoder);
						imageData = ImageDataFactory.Create(imgSt.ToArray());
						imgSt.Close();
					}
					using var mapfile = MemoryMappedFile.CreateNew(null, MapFileSize);
					nint handle = mapfile.SafeMemoryMappedFileHandle.DangerousGetHandle();
					compressor.Compress(file, handle, MapFileSize);
					using var mapstream = mapfile.CreateViewStream();
					using Image ii = Image.Load(mapstream);
					ii.Save("C:\\Users\\Tyler Parret True\\Documents\\@Works\\@Code\\Git.Private\\PicMergeToPdf\\publish\\test.png");
					mapstream.Close();
					break;
				}
				catch (UnknownImageFormatException) {
					if (!warningedFormat) {
						failed.Add(IOP.GetDirectoryName(file) ?? file);
						failed.Add("Invalid format around.");
						warningedFormat = true;
					}
				}
				catch (Exception e) {
					//failed.Add(e.GetType().FullName ?? "");
					failed.Add(file);
					failed.Add(e.Message);
				}
			}
			if (i >= files.Count) {
				return [];
			}

			using FileStream stream = new(outputfilepath, FileMode.CreateNew, FileAccess.Write);
			WriterProperties writerProperties = new();
			writerProperties.SetFullCompressionMode(true);
			writerProperties.SetCompressionLevel(CompressionConstants.BEST_COMPRESSION);

			using (PdfWriter writer = new(stream, writerProperties)) {
				using PdfDocument pdfDocument = new(writer);
				pdfDocument.GetDocumentInfo().SetKeywords(Title);

				if (imageData != null)
					AddImage(imageData, pdfDocument, pageSizeType, pagesizex, pagesizey);
				for (i++; i < files.Count; ++i) {
					SingleUpdate(id, i, files.Count);
					string file = files[i];
					try {
						try { // 尝试直接加载
							imageData = ImageDataFactory.Create(file);
						}
						catch (Exception) { // 若不支持则转码
							using Image image = Image.Load(file);
							using MemoryStream imgSt = new();
							//image.SaveAsJpeg(imgSt, encoder);
							//image.SaveAsPng(imgSt, encoder);
							//image.SaveAsTiff(imgSt);
							//image.SaveAsBmp(imgSt);
							image.SaveAsGif(imgSt, gifEncoder);
							imageData = ImageDataFactory.Create(imgSt.ToArray());
							imgSt.Close();
						}
						AddImage(imageData, pdfDocument, pageSizeType, pagesizex, pagesizey);
					}
					catch (UnknownImageFormatException) {
						if (!warningedFormat) {
							failed.Add(IOP.GetDirectoryName(file) ?? file);
							failed.Add("Invalid format around.");
							warningedFormat = true;
						}
					}
					catch (Exception e) {
						//failed.Add(e.GetType().FullName ?? "");
						failed.Add(file);
						failed.Add(e.Message);
					}
				}
			}

			SingleUpdate(id, files.Count, files.Count);

			stream.Close();

			return failed;
		}

		private static void AddImage(ImageData imageData, PdfDocument pdfDocument, int pageSizeType, float pagesizex, float pagesizey) {
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

