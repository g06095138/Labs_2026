# Комплект сдачи ЛР0–ЛР5
| Лаба | Файлы | Проверка |
|---|---|---|
| 0 | docs/lab0.md; src/Taxi.Domain; src/Taxi.Console; tests/Taxi.Tests; diagrams/domain.* | формула, примеры, консоль и тесты |
| 1 | docs/requirements.md; diagrams/use-cases.* | сценарии, 8 примеров, трассировка |
| 2 | docs/design.md; docs/data.md; diagrams/components.*, domain.*, er.* | слои, контракты, SRP/DIP, ручной проход |
| 3 | Domain, Application, Console, Tests; актуальная спецификация | оценка, сравнение, последовательные переходы, неизменность |
| 4 | Infrastructure, integration tests; docs/data.md; diagrams/er.* | SQLite, копейки, транзакции, перезапуск |
| 5 | Taxi.Web; docs/ui.md; ui-mockup.svg; README.md; docs/validation.md | сайт, сборка, тесты, сквозной сценарий |

Документы Word в docs/reports — копии для чтения; первичные документы — Markdown. Схемы: PNG для вставки, SVG для масштабирования, PUML для редактирования. Для GitLab загружать распакованные каталоги проекта, а не только ZIP. Не переносить bin/obj, локальную БД и tools.

Ранее сделанная Lab0_Taxi — отдельный вводный комплект по курсовой; он не заменяет ЛР0 этой методички. Новые лабораторные расположены отдельно и согласованы с веб-стеком.
