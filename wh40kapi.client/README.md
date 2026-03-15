# wh40kapi.client

Фронтенд-приложение (SPA) для [wh40kAPI](../README.md), собранное на **React 19 + TypeScript 5 + Vite 7**.

## Технологии

- [React 19](https://react.dev/)
- [TypeScript 5](https://www.typescriptlang.org/)
- [Vite 7](https://vitejs.dev/)
- [React Router DOM 7](https://reactrouter.com/)

## Страницы

| Маршрут | Описание |
|---------|----------|
| `/` | Стартовая страница |
| `/wahapedia` | Главная WH40K |
| `/factions` | Фракции |
| `/datasheets` | Датащиты |
| `/detachments` | Отряды |
| `/stratagems` | Стратагемы |
| `/enhancements` | Улучшения |
| `/bsdata-40k` | BSData 40K |
| `/bsdata-killteam` | BSData Kill Team |
| `/admin` | Панель администратора |

## Разработка

```sh
npm install
npm run dev
```

Vite dev server запустится на `http://localhost:51018` и будет проксировать запросы к `/api` на бэкенд ASP.NET Core.

## Сборка

```sh
npm run build
```

Готовые файлы окажутся в директории `dist/`. Для production-развёртывания используется многоэтапная Docker-сборка (`Dockerfile`) с Nginx в качестве веб-сервера.

## Линтинг

```sh
npm run lint
```
