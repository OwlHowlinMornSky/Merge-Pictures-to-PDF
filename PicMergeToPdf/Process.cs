using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Org.BouncyCastle.Utilities;

namespace PicMergeToPdf {
	public static class Process {

		public static Action<double> SingleUpdate = x => { };

		public static async Task<List<string>> ProcessAsync(string outputfilepath, List<string> files, int pageSizeType, int pagesizex, int pagesizey) {
			List<string> failed = [];
			using FileStream stream = new(outputfilepath, FileMode.CreateNew, FileAccess.Write);
			//using FileStream stream = new("test.pdf", FileMode.Create, FileAccess.Write);
			WriterProperties writerProperties = new();
			writerProperties.SetFullCompressionMode(true);
			writerProperties.SetCompressionLevel(CompressionConstants.BEST_COMPRESSION);


			using (PdfWriter writer = new(stream, writerProperties)) {
				using PdfDocument pdfDocument = new(writer);
				int cnt = 0;
				foreach (string file in files) {
					try {
						ImageData imageData;
						using (Image image = await Image.LoadAsync(file)) {
							using MemoryStream tmpImg = new();
							image.SaveAsPng(tmpImg);
							imageData = ImageDataFactory.CreatePng(tmpImg.ToArray());
						}
						/*
						using Image image = await Image.LoadAsync(file);
						var imgRgb24 = image.CloneAs<Rgb24>();
						int length = imgRgb24.Size.Width * imgRgb24.Size.Height * 3;
						byte[] bytes = new byte[length];
						imgRgb24.CopyPixelDataTo(bytes);
						ImageData imageData = ImageDataFactory.CreateRawImage(bytes);
						imageData.SetWidth(imgRgb24.Size.Width);
						imageData.SetHeight(imgRgb24.Size.Height);
						imageData.SetColorEncodingComponentsNumber(3);
						imageData.SetBpc(8);
						*/

						PageSize pageSize;
						PageSize imageSize;
						switch (pageSizeType) {
						default:
						case 1:
							imageSize = new(imgRgb24.Size.Width, imgRgb24.Size.Height);
							pageSize = imageSize;
							break;
						case 2:
							if (pagesizex == 0) {
								pagesizex = imgRgb24.Size.Width;
							}
							imageSize = new(pagesizex, 1.0f * pagesizex / imgRgb24.Size.Width * imgRgb24.Size.Height);
							pageSize = imageSize;
							break;
						case 3:
							if (pagesizex == 0 || pagesizey == 0) {
								pagesizex = imgRgb24.Size.Width;
								pagesizey = imgRgb24.Size.Height;
							}
							pageSize = new(pagesizex, pagesizey);
							float r = float.Min(
								1.0f * pagesizex / imgRgb24.Size.Width,
								1.0f * pagesizey / imgRgb24.Size.Height
							);
							imageSize = new(imgRgb24.Size.Width * r, imgRgb24.Size.Height * r);
							imageSize.SetX((pageSize.GetWidth() - imageSize.GetWidth()) / 2.0f);
							imageSize.SetY((pageSize.GetHeight() - imageSize.GetHeight()) / 2.0f);
							break;
						}
						PdfPage page = pdfDocument.AddNewPage(pageSize);
						PdfCanvas canvas = new(page, true);
						canvas.AddImageFittedIntoRectangle(imageData, imageSize, true);
					}
					catch (Exception) {
						failed.Add(file);
					}
					cnt++;
					SingleUpdate(90.0 * cnt / files.Count);
				}
			}
			SingleUpdate(90);
			stream.Close();
			SingleUpdate(100);
			return failed;
		}
	}
}

