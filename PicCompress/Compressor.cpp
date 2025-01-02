
#include "Compressor.h"

#include <libiodine/libiodine.h>

#include <strsafe.h>

namespace {

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

PicCompress::Compressor::Compressor(System::IntPtr handle, System::Int64 maxlen) {
	if ((void*)handle == nullptr || maxlen < 1) {
		throw gcnew InvalidOperationException("Invalid Mapping File.");
	}
	r_hmapping = (HANDLE)handle;
	m_view = MapViewOfFile(r_hmapping, FILE_MAP_WRITE, 0, 0, 0);
	if (nullptr == m_view) {
		LPVOID description = ::WinCheckError(L"Failed to Open Map View");
		auto excep = gcnew InvalidOperationException(gcnew System::String((LPWSTR)description));
		LocalFree(description);
		throw excep;
	}
	m_maxlen = maxlen;
}

PicCompress::Compressor::~Compressor() {
	BOOL res = UnmapViewOfFile(m_view);
	if (0 == res) {
		LPVOID description = ::WinCheckError(L"Failed to Open Map View");
		auto excep = gcnew InvalidOperationException(gcnew System::String((LPWSTR)description));
		LocalFree(description);
		throw excep;
	}
}

System::Int32 PicCompress::Compressor::Compress(System::String^ file) {
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
	parameters.width = 1680;

	CSI_Result res = csi_convert_into(cstr, m_view, m_maxlen, CSI_SupportedFileTypes::Jpeg, &parameters);

	delete[] cstr;
	if (!res.success) {
		throw gcnew InvalidOperationException(gcnew System::String(res.error_message));
	}
	if (res.code < 1 || res.code > 2147483600ull) {
		throw gcnew InsufficientMemoryException();
	}
	return (System::Int32)res.code;
}

System::Int32 PicCompress::Compressor::CompressFrom(System::IntPtr handle, System::Int64 len) {
	if ((void*)handle == nullptr || len < 1) {
		throw gcnew ArgumentException("Invalid Input File.");
	}
	void* inview = MapViewOfFile((HANDLE)handle, FILE_MAP_READ, 0, 0, 0);
	if (nullptr == inview) {
		LPVOID description = ::WinCheckError(L"Failed to Open Map View");
		auto excep = gcnew InvalidOperationException(gcnew System::String((LPWSTR)description));
		LocalFree(description);
		throw excep;
	}

	CSI_Parameters parameters = {};
	parameters.keep_metadata = false;
	parameters.jpeg_quality = 80;
	parameters.jpeg_progressive = true;
	parameters.width = 1680;

	CSI_Result res = csi_convert_fromto(inview, len, m_view, m_maxlen, CSI_SupportedFileTypes::Jpeg, &parameters);

	if (!res.success) {
		throw gcnew InvalidOperationException(gcnew System::String(res.error_message));
	}
	if (res.code < 1 || res.code > 2147483600ull) {
		throw gcnew InsufficientMemoryException();
	}
	return (System::Int32)res.code;
}
