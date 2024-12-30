
#include "Compressor.h"
//#include <msclr/marshal_cppstd.h>

#include <libiodine/libiodine.h>
#define WIN32_LEAN_AND_MEAN (1)
#include <Windows.h>

PicCompress::Compressor::Compressor() {}

PicCompress::Compressor::~Compressor() {}

System::Void PicCompress::Compressor::Compress(System::String^ file, System::IntPtr handle, System::Int64 maxlen) {
	if (maxlen < 1) {
		throw gcnew InvalidOperationException("Invalid maxlen.");
	}

	cli::array<wchar_t>^ wArray = file->ToCharArray();
	cli::array<unsigned char, 1>^ arr = System::Text::Encoding::UTF8->GetBytes(wArray);

	int len = arr->Length;
	char* cstr = new char[len + 2];
	System::IntPtr pcstr(cstr);
	System::Runtime::InteropServices::Marshal::Copy(arr, 0, pcstr, len);
	cstr[len] = 0;
	cstr[len + 1] = 0;

	CSI_Parameters parameters = {};
	parameters.keep_metadata = false;
	parameters.jpeg_quality = 80;
	parameters.jpeg_progressive = true;
	//parameters.png_quality = 80;

	HANDLE hmap = (HANDLE)handle;
	void* view = MapViewOfFile(hmap, FILE_MAP_WRITE, 0, 0, 0);
	if (nullptr == view) {
		delete[] cstr;
		throw gcnew InvalidOperationException("Failed to Open Map View.");
	}

	CSI_Result res = csi_convert_into(cstr, view, maxlen, CSI_SupportedFileTypes::Jpeg, &parameters);

	delete[] cstr;
	if (!res.success) {
		throw gcnew InvalidOperationException(gcnew System::String(res.error_message));
	}
	return;
}
