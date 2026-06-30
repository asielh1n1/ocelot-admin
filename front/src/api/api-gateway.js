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