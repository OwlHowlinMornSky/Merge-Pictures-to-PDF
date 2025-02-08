#include "WinFormat.h"

#define WIN32_LEAN_AND_MEAN (1)
#include <Windows.h>
#include <strsafe.h>

MapView::MapView(void* handleToFileMapping, bool isWrite) {
	m_view = MapViewOfFile(handleToFileMapping, isWrite ? FILE_MAP_WRITE : FILE_MAP_READ, 0, 0, 0);
}

MapView::~MapView() {
	if (m_view)
		UnmapViewOfFile(m_view);
}

void* MapView::GetView() {
	return m_view;
}

LocalString::LocalString(void* localMem) {
	m_str = localMem;
}

LocalString::~LocalString() {
	if (m_str)
		LocalFree(m_str);
}

const wchar_t* LocalString::GetString() {
	return (wchar_t*)m_str;
}

LocalString WinCheckError(wchar_t* lpszFunction) noexcept {
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
	return LocalString(lpMsgBuf);
}
