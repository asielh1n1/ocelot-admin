import axios from 'axios'
const api = axios.create({ baseURL: process.env.URL_API })

export default class LoginApi {

    login(username, password) {
        return new Promise((resolve, reject) => {
            api.post('/login', { username, password })
            .then((response) => {
                resolve(response.data)
              })
              .catch((error) => {
                reject(error)
              })
        })
    }
}