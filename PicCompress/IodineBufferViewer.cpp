#include "IodineBufferViewer.h"

using namespace System::Runtime::InteropServices;

namespace PicCompress {

IodineBufferViewer::IodineBufferViewer(CByteArray buffer) {
	if (buffer.length >= System::Int32::MaxValue) {
		iod_free_buffer(buffer);
		throw gcnew System::InsufficientMemoryException("The buffer from iodine is too large");
	}
	m_buffer = buffer.data;
	m_length = static_cast<int>(buffer.length);
}

IodineBufferViewer::~IodineBufferViewer() {
	this->!IodineBufferViewer();
}

IodineBufferViewer::!IodineBufferViewer() {
	if (m_buffer != nullptr) {
		CByteArray buffer{};
		buffer.data = m_buffer;
		buffer.length = m_length;
		iod_free_buffer(buffer);

		m_buffer = nullptr;
		m_length = 0;
	}
}

System::Byte* IodineBufferViewer::GetNativePointer() {
	return m_buffer;
}

array<System::Byte>^ IodineBufferViewer::ToArray() {
	// 检查长度是否超过 int 最大值（byte[] 索引使用 int）
	if (m_length > System::Int32::MaxValue)
		throw gcnew System::InvalidOperationException(
			"Buffer length exceeds Int32.MaxValue, cannot copy entire buffer to byte[]."
		);

	int len = (int)m_length;
	array<System::Byte>^ managed = gcnew array<System::Byte>(len);

	// 使用 Marshal.Copy 高效复制
	Marshal::Copy(System::IntPtr(m_buffer), managed, 0, len);
	return managed;
}

array<System::Byte>^ IodineBufferViewer::ToArray(int offset, int count) {
	if (offset < 0 || count < 0 || offset + count > m_length)
		throw gcnew System::ArgumentOutOfRangeException();

	array<System::Byte>^ managed = gcnew array<System::Byte>(count);
	Marshal::Copy(System::IntPtr(m_buffer + offset), managed, 0, count);
	return managed;
}

}
