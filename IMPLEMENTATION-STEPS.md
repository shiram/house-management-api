Implementation steps so far

Phase 0 - Foundation (completed)
- Scaffoled Angular 21 app at frontend/house-management-web
- Enabled strict TypeScript, standalone components, routing
- Added environment configs
- Installed Bootstrap and Bootstrap Icons
- Implemented core services:
  - ApiService (central HTTP wrapper)
  - AuthService (JWT storage, token parsing, current user)
  - TokenInterceptor (attaches Bearer token, handles 401)
  - NotificationsService (placeholder loader)
  - UiStateService (sidebar/mobile state)
- Implemented layout:
  - ShellComponent (app shell)
  - NavbarComponent (user menu, notifications, mobile toggle)
  - SidebarComponent (navigation groups, active highlighting)
- Implemented basic auth UI:
  - LoginComponent with validation, loading and error handling
- Added Dashboard placeholder and lazy route
- Updated TASKS.md to mark T007/T008 complete

Phase 1 / 2 - Next steps completed in this pass
- Improved TokenInterceptor to correctly set Authorization header
- Implemented mobile sidebar overlay and toggling via UiStateService
- Added IMPLEMENTATION-STEPS.md documenting progress

Notes / To do next
- Implement User management screens (list, create, edit)
- Implement HouseHelp directory screens
- Add toasts/confirmation modal components
- Add unit tests for services and guards
- Tune bundle size and lazy-loading of feature modules

If any required backend endpoints are missing when implementing a feature, implementation will stop and the missing API contract will be documented (HTTP method, path, request and response).