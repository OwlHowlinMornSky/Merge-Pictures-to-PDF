
namespace PicMerge {
	internal static class FileType {

		internal enum Type {
			Unknown = 0,
			JPEG,
			PNG,
			GIF,
			TIFF,
			WEBP,
			BMP,
			//PBM,
			//TGA,
		}

		internal static Type CheckType(Stream file) {
			Type res = Type.Unknown;

			BinaryReader br = new(file);

			var b = br.ReadBytes(8);

			if (b[0] == 0xFF && b[1] == 0xD8) {
				res = Type.JPEG;
			}
			else if (b[0] == 0x42 && b[1] == 0x4D) {
				res = Type.BMP;
			}
			else if (b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) {
				res = Type.GIF;
			}
			else if (b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46) {
				res = Type.WEBP;
			}
			else if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) {
				res = Type.PNG;
			}
			else if (b[0] == b[1] && b[1] == 0x49 && b[2] == 0x2A && b[3] == 0x00) {
				res = Type.TIFF;
			}
			else if (b[0] == b[1] && b[1] == 0x4D && b[2] == 0x00 && b[3] == 0x2A) {
				res = Type.TIFF;
			}

			return res;
		}

	}
}
