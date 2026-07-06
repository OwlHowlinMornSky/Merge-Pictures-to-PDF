#pragma once

#include <string>

/**
 * @brief 自动管理 一个 对文件的 内存映射
 */
class MapView {
public:
	MapView(void* handleToFileMapping, bool isWrite = false);
	~MapView(); // 自动释放映射
	/**
	 * @brief 获取映射
	 * @return 映射的首地址
	 */
	void* GetView();
private:
	void* m_view;
};

std::wstring WinCheckError(std::wstring_view desciption) noexcept;
