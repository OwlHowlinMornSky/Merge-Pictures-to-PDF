#include "WinFormat.h"

#include <format>

#define WIN32_LEAN_AND_MEAN (1)
#include <Windows.h>
#include <strsafe.h>

MapView::MapView(void* handleToFileMapping, bool isWrite) {
	// 构造时 自动创建 映射
	m_view = MapViewOfFile(handleToFileMapping, isWrite ? FILE_MAP_WRITE : FILE_MAP_READ, 0, 0, 0);
}

MapView::~MapView() {
	// 析构时 自动释放 映射
	if (m_view)
		UnmapViewOfFile(m_view);
}

void* MapView::GetView() {
	return m_view;
}

std::wstring WinCheckError(std::wstring_view desciption) noexcept {
	LPWSTR lpMsgBuf = NULL;
	DWORD dw = GetLastError();

	DWORD ex = FormatMessageW(
		FORMAT_MESSAGE_ALLOCATE_BUFFER |
		FORMAT_MESSAGE_FROM_SYSTEM |
		FORMAT_MESSAGE_IGNORE_INSERTS,
		NULL,
		dw,
		MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
		(LPWSTR)&lpMsgBuf,
		0, NULL
	);

	if (0 == ex) {
		return {};
	}

	try {
		std::wstring res = std::format(L"{0}: {1} ({2}).", desciption, lpMsgBuf, dw);
		LocalFree(lpMsgBuf);
		return res;
	}
	catch (...) {
		;
	}

	LocalFree(lpMsgBuf);
	return {};
}
