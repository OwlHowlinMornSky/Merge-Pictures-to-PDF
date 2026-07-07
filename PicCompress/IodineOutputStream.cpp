#include "IodineOutputStream.h"

namespace PicCompress {

IodineOutputStream::~IodineOutputStream() {
	this->!IodineOutputStream();
}

IodineOutputStream::!IodineOutputStream() {
	if (m_buffer != nullptr) {
		CByteArray buffer{};
		buffer.data = m_buffer;
		buffer.length = m_length;
		iod_free_buffer(buffer);
	}
	m_buffer = nullptr;
	m_length = 0;
	m_position = 0;
}

IodineOutputStream::IodineOutputStream(CByteArray buffer) :
	m_position(0) {
	if (buffer.length >= System::Int64::MaxValue) {
		iod_free_buffer(buffer);
		throw gcnew System::InsufficientMemoryException("The buffer from iodine is too large");
	}
	m_buffer = buffer.data;
	m_length = static_cast<int>(buffer.length);
}

}
