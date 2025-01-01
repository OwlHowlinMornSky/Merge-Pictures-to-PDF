using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicMerge {
	public interface Merger : IDisposable {

		public virtual List<string> Process(string outputfilepath, List<string> files, string title = "") {
			return [];
		}

		public static Merger Create(bool filepara, Action finish1img, int pageSizeType = 2, int pagesizex = 0, int pagesizey = 0, bool compress = true) {
			return filepara ? new FileParallel(finish1img, pageSizeType, pagesizex, pagesizey, compress) : new Main(finish1img, pageSizeType, pagesizex, pagesizey, compress);
		}

	}
}
