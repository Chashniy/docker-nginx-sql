FROM node:20-alpine AS builder
WORKDIR /app

COPY package*.json ./
RUN npm config set fund false && npm install

COPY . .

RUN npx tsc -b && npx vite build

FROM nginx:stable-alpine

COPY nginx.conf /etc/nginx/nginx.conf
COPY --from=builder /app/dist /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]