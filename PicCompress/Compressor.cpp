
#include "Compressor.h"

#include <libiodine/libiodine.h>

#define WIN32_LEAN_AND_MEAN (1)
#include <Windows.h>
#include <strsafe.h>

namespace {

class MapView {
public:
	MapView(HANDLE hfilemapping) {
		m_view = MapViewOfFile((HANDLE)hfilemapping, FILE_MAP_READ, 0, 0, 0);
	}
	~MapView() {
		if (m_view)
			UnmapViewOfFile(m_view);
	}
	void* GetView() {
		return m_view;
	}
private:
	void* m_view;
};

LPVOID WinCheckError(LPCWSTR lpszFunction) noexcept {
	LPVOID lpMsgBuf = NULL;
	LPVOID lpDisplayBuf = NULL;
	DWORD dw = GetLastError();

	FormatMessageW(
		FORMAT_MESSAGE_ALLOCATE_BUFFER |
		FORMAT_MESSAGE_FROM_SYSTEM |
		FORMAT_MESSAGE_IGNORE_INSERTS,
		NULL,
		dw,
		MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
		(LPWSTR)&lpMsgBuf,
		0, NULL
	);

	lpDisplayBuf = (LPVOID)LocalAlloc(
		LMEM_ZEROINIT,
		(static_cast<SIZE_T>(lstrlenW((LPCWSTR)lpMsgBuf)) +
			lstrlenW((LPCWSTR)lpszFunction) + 50) * sizeof(WCHAR)
	);
	if (lpDisplayBuf) {
		StringCchPrintfW(
			(LPWSTR)lpDisplayBuf,
			LocalSize(lpDisplayBuf) / sizeof(WCHAR),
			L"%s: %s (%d).",
			lpszFunction, (LPCWSTR)lpMsgBuf, dw
		);
		LocalFree(lpMsgBuf);
		lpMsgBuf = lpDisplayBuf;
	}
	return lpMsgBuf;
}

}

PicCompress::Compressor::Compressor(System::IntPtr houtfile, System::Int64 oFileMaxLen) {
	if ((HANDLE)houtfile == NULL || oFileMaxLen < 1) {
		throw gcnew InvalidOperationException("Invalid Output Mapping File.");
	}
	m_viewOfOutFile = MapViewOfFile((HANDLE)houtfile, FILE_MAP_WRITE, 0, 0, 0);
	if (nullptr == m_viewOfOutFile) {
		LPVOID description = ::WinCheckError(L"Failed to Map Output View");
		auto ex = gcnew InvalidOperationException(gcnew System::String((LPWSTR)description));
		LocalFree(description);
		throw ex;
	}
	m_oFileMaxLen = oFileMaxLen;
}

PicCompress::Compressor::~Compressor() {
	BOOL res = UnmapViewOfFile(m_viewOfOutFile);
	if (0 == res) {
		LPVOID description = ::WinCheckError(L"Failed to Unmap Output View");
		auto ex = gcnew InvalidOperationException(gcnew System::String((LPWSTR)description));
		LocalFree(description);
		throw ex;
	}
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
	if ((HANDLE)hinfile == NULL || iFileLen < 1) {
		throw gcnew ArgumentException("Invalid Input Maping File.");
	}
	::MapView input_file_view((HANDLE)hinfile);
	void* inview = input_file_view.GetView();
	if (nullptr == inview) {
		LPVOID description = ::WinCheckError(L"Failed to Map Input View");
		auto ex = gcnew InvalidOperationException(gcnew System::String((LPWSTR)description));
		LocalFree(description);
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
		res = csi_compress_fromto(inview, iFileLen, m_viewOfOutFile, m_oFileMaxLen, &parameters);
		break;
	case 1:
		res = csi_convert_fromto(inview, iFileLen, m_viewOfOutFile, m_oFileMaxLen, CSI_SupportedFileTypes::Jpeg, &parameters);
		break;
	case 2:
		res = csi_convert_fromto(inview, iFileLen, m_viewOfOutFile, m_oFileMaxLen, CSI_SupportedFileTypes::Png, &parameters);
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
