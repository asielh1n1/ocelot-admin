import { api } from '../boot/axios'

export default class ApiGateway {

    getConfig() {
        return new Promise((resolve, reject) => {
            api.get('/data')
            .then((response) => {
                resolve(response.data)
              })
              .catch((error) => {
                reject(error)
              })
        })
    }
    getPlugins() {
      return new Promise((resolve, reject) => {
        api.get('/plugins')
          .then((response) => resolve(response.data))
          .catch((error) => reject(error))
      })
    }
    uploadPlugin(file) {
      const data = new FormData()
      data.append('file', file)
      return new Promise((resolve, reject) => {
        api.post('/plugins', data, { headers: { 'Content-Type': 'multipart/form-data' } })
          .then((response) => resolve(response.data))
          .catch((error) => reject(error))
      })
    }
    deletePlugin(fileName) {
      return new Promise((resolve, reject) => {
        api.delete('/plugins', { params: { fileName } })
          .then((response) => resolve(response.data))
          .catch((error) => reject(error))
      })
    }
    saveConfig(data) {
      return new Promise((resolve, reject) => {
        api.post('/data', data)
          .then((response) => {
              resolve(response.data.data)
            })
            .catch((error) => {
              reject(error)
            })
      })
  }
}