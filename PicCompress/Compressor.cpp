
#include "Compressor.h"

#include <libiodine/libiodine.h>

PicCompress::Compressor::Compressor(System::IntPtr houtfile, System::Int64 oFileMaxLen) {
	if (houtfile.ToPointer() == nullptr || oFileMaxLen < 1) {
		throw gcnew InvalidOperationException("Invalid Output Mapping File.");
	}
	m_viewOfOutFile = new MapView(houtfile.ToPointer(), true);
	m_view = m_viewOfOutFile->GetView();
	if (nullptr == m_view) {
		LocalString description = ::WinCheckError(L"Failed to Map Output View");
		auto ex = gcnew InvalidOperationException(gcnew System::String(description.GetString()));
		throw ex;
	}
	m_oFileMaxLen = oFileMaxLen;
}

PicCompress::Compressor::~Compressor() {
	m_view = nullptr;
	delete m_viewOfOutFile;
	m_viewOfOutFile = nullptr;
}

System::Int32 PicCompress::Compressor::CompressFrom(
	System::IntPtr hinfile,
	System::Int64 iFileLen,
	System::Int32 targetType,
	System::Int32 quality,
	bool resize,
	int width,
	int height,
	int shortSide,
	int longSide,
	bool reduceBtPowOf2
) {
	if (hinfile.ToPointer() == NULL || iFileLen < 1) {
		throw gcnew ArgumentException("Invalid Input Maping File.");
	}
	MapView input_file_view(hinfile.ToPointer());
	void* inview = input_file_view.GetView();
	if (nullptr == inview) {
		LocalString description = ::WinCheckError(L"Failed to Map Input View");
		auto ex = gcnew InvalidOperationException(gcnew System::String(description.GetString()));
		throw ex;
	}

	CSI_Parameters parameters = {};
	parameters.keep_metadata = false;
	parameters.jpeg_quality = quality;
	parameters.jpeg_progressive = true;
	parameters.png_quality = quality;
	if (resize) {
		parameters.width = width;
		parameters.height = height;
		parameters.short_side_pixels = shortSide;
		parameters.long_size_pixels = longSide;
		parameters.reduce_by_power_of_2 = reduceBtPowOf2;
		parameters.allow_magnify = false;
	}

	CSI_Result res;
	switch (targetType) {
	case 0:
		res = csi_compress_fromto(inview, iFileLen, m_view, m_oFileMaxLen, &parameters);
		break;
	case 1:
		res = csi_convert_fromto(inview, iFileLen, m_view, m_oFileMaxLen, CSI_SupportedFileTypes::Jpeg, &parameters);
		break;
	case 2:
		res = csi_convert_fromto(inview, iFileLen, m_view, m_oFileMaxLen, CSI_SupportedFileTypes::Png, &parameters);
		break;
	default:
		throw gcnew System::ArgumentException(String::Format("Unknown target type: {0}", targetType));
	}

	if (!res.success) {
		throw gcnew InvalidOperationException(gcnew System::String(res.error_message));
	}
	if (res.code < 1 || res.code > 2147483600ull) {
		throw gcnew InsufficientMemoryException(String::Format("Code: {0}", res.code));
	}
	return (System::Int32)res.code;
}
