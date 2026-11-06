import api from './api.js'

export async function getUsers() {
  const response = await api.get('/users')
  return response.data
}

export async function getUserById(id) {
  const response = await api.get(`/users/${id}`)
  return response.data
}

export async function updateUser(id, user) {
  const response = await api.put(`/users/${id}`, user)
  return response.data
}

export async function deleteUser(id) {
  const response = await api.delete(`/users/${id}`)
  return response.data
}

// The API has no admin "create user" endpoint; registration always creates a
// Member. The returned token is intentionally ignored so the Admin's own
// session is untouched.
export async function createUser({ fullName, email, password }) {
  const response = await api.post('/auth/register', { fullName, email, password })
  return response.data
}
