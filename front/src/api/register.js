import axios from 'axios'
const api = axios.create({ baseURL: process.env.URL_API })

export default class RegisterApi {

    register(data) {
        return new Promise((resolve, reject) => {
            api.post('/register', data)
            .then((response) => {
                resolve(response.data)
              })
              .catch((error) => {
                reject(error)
              })
        })
    }
}
