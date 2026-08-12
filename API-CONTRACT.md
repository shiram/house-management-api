# House Management API Contract

## Environment

### Development API

Base URL:

https://localhost:44368/api

> Replace the IP and port with the actual development machine/API address.

Angular must NEVER hardcode the API URL inside individual services.

The API base URL must be configured through Angular environment configuration.

Example:

environment.development.ts

apiUrl: 'https://localhost:44368/api'

---

# Authentication

## Login

POST:

/auth/login

Request:

{
  "email": "user@example.com",
  "password": "Password123!"
}

Response:

{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwidW5pcXVlX25hbWUiOiJFZHdhcmQiLCJlbWFpbCI6Im1vc2RldmVsb3BlcnNAZ21haWwuY29tIiwiaHR0cDovL3NjaGVtYXMubWljcm9zb2Z0LmNvbS93cy8yMDA4LzA2L2lkZW50aXR5L2NsYWltcy9yb2xlIjoiaG91c2VoZWxwIiwiZXhwIjoxNzg2NTM5MjY0LCJpc3MiOiJIb3VzZU1hbmFnZW1lbnQiLCJhdWQiOiJIb3VzZU1hbmFnZW1lbnQifQ.fKq4LaTSHyWpC_yaUVbMRzzlQRTR5SqeC8mgj1Ag7g8",
  "userName": "Edward",
  "email": "mosdevelopers@gmail.com",
  "role": "househelp"
}

## Register

/Auth/register

Request:

{
  "userName": "Edward",
  "email": "mosdevelopers@gmail.com",
  "password": "1234"
}

Response:

{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwidW5pcXVlX25hbWUiOiJFZHdhcmQiLCJlbWFpbCI6Im1vc2RldmVsb3BlcnNAZ21haWwuY29tIiwiaHR0cDovL3NjaGVtYXMubWljcm9zb2Z0LmNvbS93cy8yMDA4LzA2L2lkZW50aXR5L2NsYWltcy9yb2xlIjoiaG91c2VoZWxwIiwiZXhwIjoxNzg2NTM5MjY0LCJpc3MiOiJIb3VzZU1hbmFnZW1lbnQiLCJhdWQiOiJIb3VzZU1hbmFnZW1lbnQifQ.fKq4LaTSHyWpC_yaUVbMRzzlQRTR5SqeC8mgj1Ag7g8",
  "userName": "Edward",
  "email": "mosdevelopers@gmail.com",
  "role": "househelp"
}

---

# Standard API Response

Most APIs return:

{
  "data": {},
  "statusCode": 200,
  "message": "Success",
  "requestId": "uuid",
  "responseDateTime": "2026-05-25T23:33:36.1819468+03:00",
  "error": null
}

Error:

{
  "data": null,
  "statusCode": 400,
  "message": "Validation failed",
  "requestId": "uuid",
  "responseDateTime": "2026-05-25T23:33:36.1819468+03:00",
  "error": {
    "code": 400,
    "message": "Validation failed",
    "details": "..."
  }
}

---

# HouseHelps

## Get house helps

GET:

/HouseHelps

Response:

{
  "data": [
	{
		"id": 1,
		"userId": 1,
		"firstName": "Edward",
		"lastName": "Dos Santos",
		"phone": "25785171079",
		"city": "Kampala",
		"address": "Luzira Bbina",
		"isActive": true,
		"skills": [
			"Laundry",
			"Cooking",
			"Cleaning",
			"Nanny"
		]
	}
],
  "statusCode": 200,
  "message": "Fetched househelps",
  "requestId": "uuid",
  "responseDateTime": "...",
  "error": null
}

POST:
/HouseHelps

Request:
{
  "userId": 1,
  "firstName": "Edward",
  "lastName": "Dos Santos",
  "phone": "25785171079",
  "city": "Kampala",
  "address": "Luzira Bbina",
  "skills": [
    "Laundry", "Cooking", "Cleaning", "Nanny"
  ]
}

Response:

{
  "data": {
	"id": 1,
	"userId": 1,
	"firstName": "Edward",
	"lastName": "Dos Santos",
	"phone": "25785171079",
	"city": "Kampala",
	"address": "Luzira Bbina",
	"isActive": true,
	"skills": [
		"Laundry",
		"Cooking",
		"Cleaning",
		"Nanny"
	]
  },
  "statusCode": 200,
  "message": "Fetched househelps",
  "requestId": "uuid",
  "responseDateTime": "...",
  "error": null
}