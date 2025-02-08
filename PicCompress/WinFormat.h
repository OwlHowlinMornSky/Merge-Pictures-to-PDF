#pragma once

class MapView {
public:
	MapView(void* handleToFileMapping, bool isWrite = false);
	~MapView();
	void* GetView();
private:
	void* m_view;
};

class LocalString {
public:
	LocalString(void* localMem);
	~LocalString();
	const wchar_t* GetString();
private:
	void* m_str;
};

LocalString WinCheckError(wchar_t* lpszFunction) noexcept;
