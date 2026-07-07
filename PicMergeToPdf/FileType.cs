
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
			var oldpos = file.Position;

			byte[] b = new byte[8];
			if (file.Read(b, 0, 8) != 8)
				return Type.Unknown;
			var res = CheckType(b);

			file.Position = oldpos;
			return res;
		}

		internal static Type CheckType(byte[] b) {

			if (b.Length < 2)
				return Type.Unknown;

			if (b[0] == 0xFF && b[1] == 0xD8)
				return Type.JPEG;

			if (b[0] == 'B' && b[1] == 'M')
				return Type.BMP;


			if (b.Length < 3)
				return Type.Unknown;

			if (b[0] == 'G' && b[1] == 'I' && b[2] == 'F')
				return Type.GIF;


			if (b.Length < 4)
				return Type.Unknown;

			if (b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F')
				return Type.WEBP;

			if (b[0] == b[1] && b[1] == 'I' && b[2] == 0x2A && b[3] == 0x00)
				return Type.TIFF;

			if (b[0] == b[1] && b[1] == 'M' && b[2] == 0x00 && b[3] == 0x2A)
				return Type.TIFF;


			if (b.Length < 8)
				return Type.Unknown;

			if (b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G' && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
				return Type.PNG;


			return Type.Unknown;
		}

	}
}
