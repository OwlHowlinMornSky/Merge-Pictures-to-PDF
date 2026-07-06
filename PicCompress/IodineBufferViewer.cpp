#include "IodineBufferViewer.h"

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
		buffer.length = static_cast<uintptr_t>(m_length);
		c_free_byte_array(buffer);

		m_buffer = nullptr;
		m_length = 0;
	}
}

System::Byte* IodineBufferViewer::GetNativePointer() {
	return m_buffer;
}
