using System.Buffers;

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
			ZIP,
			_7ZIP,
			RAR,
		}

		internal static Type CheckType(Stream file) {
			byte[] b = ArrayPool<byte>.Shared.Rent(8);
			try {
				if (file.Read(b, 0, 8) != 8)
					return Type.Unknown;
				return CheckType(b);
			}
			finally {
				ArrayPool<byte>.Shared.Return(b, true);
			}
		}

		internal static Type CheckType(byte[] b) {
			Type res = Type.Unknown;

			if (b[0] == 0xFF && b[1] == 0xD8) {
				res = Type.JPEG;
			}
			else if (b[0] == 'B' && b[1] == 'M') {
				res = Type.BMP;
			}
			else if (b[0] == 'G' && b[1] == 'I' && b[2] == 'F') {
				//res = Type.GIF;
				res = Type.Unknown;
			}
			else if (b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F') {
				res = Type.WEBP;
			}
			else if (b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G' && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) {
				res = Type.PNG;
			}
			else if (b[0] == b[1] && b[1] == 'I' && b[2] == 0x2A && b[3] == 0x00) {
				res = Type.TIFF;
			}
			else if (b[0] == b[1] && b[1] == 'M' && b[2] == 0x00 && b[3] == 0x2A) {
				res = Type.TIFF;
			}
			else if (b[0] == 'P' && b[1] == 'K' && b[2] == 0x03 && b[3] == 0x04) {
				res = Type.ZIP;
			}
			else if (b[0] == 'R' && b[1] == 'a' && b[2] == 'r' && b[3] == '!') {
				res = Type.RAR;
			}
			else if (b[0] == '7' && b[1] == 'z' && b[2] == 0xBC && b[3] == 0xAF && b[4] == 0x27 && b[5] == 0x1C) {
				res = Type._7ZIP;
			}

			return res;
		}

	}
}
