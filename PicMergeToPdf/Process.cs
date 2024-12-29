﻿using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf;
using SixLabors.ImageSharp;
using IOP = System.IO.Path;

namespace PicMergeToPdf {
	public static class Process {

		public static Action<int> SingleUpdate = x => { };

		public static List<string> ProcessFunc(string outputfilepath, List<string> files, int pageSizeType, float pagesizex, float pagesizey, string Title = "") {
			List<string> failed = [];
			using MemoryStream stream = new();
			WriterProperties writerProperties = new();
			writerProperties.SetFullCompressionMode(true);
			writerProperties.SetCompressionLevel(CompressionConstants.BEST_COMPRESSION);

			SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder encoder = new() {
				SkipMetadata = true,
				Quality = 80,
				ColorType = SixLabors.ImageSharp.Formats.Jpeg.JpegEncodingColor.Rgb,
				Interleaved = false
			};

			int imgCnt = 0;
			bool warningedFormat = false;

			using (PdfWriter writer = new(stream, writerProperties)) {
				using PdfDocument pdfDocument = new(writer);
				pdfDocument.GetDocumentInfo().SetKeywords(Title);

				int cnt = 0;
				foreach (string file in files) {
					try {
						ImageData imageData;
						try { // 尝试直接加载
							imageData = ImageDataFactory.Create(file);
						}
						catch (Exception) { // 若不支持则转码
							using Image image = Image.Load(file);  // 压缩所有图像
							using MemoryStream tmpImg = new();
							image.SaveAsJpeg(tmpImg, encoder);
							imageData = ImageDataFactory.Create(tmpImg.ToArray());
							tmpImg.Close();
						}
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
						pageSize.SetWidth(pageSize.GetWidth() + 5.0f);
						pageSize.SetHeight(pageSize.GetHeight() + 5.0f);
						PdfPage page = pdfDocument.AddNewPage(pageSize);
						PdfCanvas canvas = new(page, true);
						canvas.AddImageFittedIntoRectangle(imageData, imageSize, true);
						imgCnt++;
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
					cnt++;
					SingleUpdate(cnt);
				}
			}
			if (imgCnt > 0) {
				using FileStream fstream = new(outputfilepath, FileMode.CreateNew, FileAccess.Write);
				stream.CopyTo(fstream);
				fstream.Close();
			}
			stream.Close();
			return failed;
		}
	}
}

