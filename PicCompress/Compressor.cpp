
#include "Compressor.h"

#include <libiodine/libiodine.h>

#define WIN32_LEAN_AND_MEAN (1)
#include <Windows.h>
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

System::Int32 PicCompress::Compressor::Compress(System::String^ pathOfInFile) {
	cli::array<wchar_t>^ wArray = pathOfInFile->ToCharArray();
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
	parameters.width = 2520;
	parameters.reduce_by_power_of_2 = true;

	CSI_Result res = csi_convert_into(cstr, m_viewOfOutFile, m_oFileMaxLen, CSI_SupportedFileTypes::Jpeg, &parameters);

	delete[] cstr;
	if (!res.success) {
		throw gcnew InvalidOperationException(gcnew System::String(res.error_message));
	}
	if (res.code < 1 || res.code > 2147483600ull) {
		throw gcnew InsufficientMemoryException();
	}
	return (System::Int32)res.code;
}

System::Int32 PicCompress::Compressor::CompressFrom(System::IntPtr hinfile, System::Int64 iFileLen) {
	if ((HANDLE)hinfile == NULL || iFileLen < 1) {
		throw gcnew ArgumentException("Invalid Input Maping File.");
	}
	void* inview = MapViewOfFile((HANDLE)hinfile, FILE_MAP_READ, 0, 0, 0);
	if (nullptr == inview) {
		LPVOID description = ::WinCheckError(L"Failed to Map Input View");
		auto ex = gcnew InvalidOperationException(gcnew System::String((LPWSTR)description));
		LocalFree(description);
		throw ex;
	}

	CSI_Parameters parameters = {};
	parameters.keep_metadata = false;
	parameters.jpeg_quality = 80;
	parameters.jpeg_progressive = true;
	parameters.width = 2520;
	parameters.reduce_by_power_of_2 = true;

	CSI_Result res = csi_convert_fromto(inview, iFileLen, m_viewOfOutFile, m_oFileMaxLen, CSI_SupportedFileTypes::Jpeg, &parameters);

	if (0 == UnmapViewOfFile(inview)) {
		LPVOID description = ::WinCheckError(L"Failed to Unmap Input View");
		auto ex = gcnew InvalidOperationException(gcnew System::String((LPWSTR)description));
		LocalFree(description);
		throw ex;
	}

	if (!res.success) {
		throw gcnew InvalidOperationException(gcnew System::String(res.error_message));
	}
	if (res.code < 1 || res.code > 2147483600ull) {
		throw gcnew InsufficientMemoryException();
	}
	return (System::Int32)res.code;
}
