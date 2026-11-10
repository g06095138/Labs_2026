# Проверка комплектности ЛР2 и ЛР3
| Пункт | Где выполнен | Состояние |
|---|---|---|
| ЛР2-А1 | design.md §2; components.*; отдельные проекты Domain/Application/Console | Четыре слоя спроектированы; Infrastructure/UI финансового модуля — будущие |
| ЛР2-А2 | IPlanRepository в Application; components.* | Контракт есть, реализация SQLite в ЛР4 |
| ЛР2-Д1 | components.puml/svg/png/pdf | Зависимости направлены к Domain/контрактам |
| ЛР2-Д2 | domain.puml/svg/png/pdf | План, три месяца, результаты, расчёт, основные операции |
| ЛР2-Д3 | design.md §5 | Прогноз/сравнение/сохранение/список/открытие: входы, результаты, ошибки |
| ЛР2-Д4 | design.md §6 | SRP и DIP объяснены на проекте такси |
| ЛР2-Д5 | data.md; er.puml/svg/png/pdf | Поля, составной PK/FK и точная кратность 1:3, без SQL-типов |
| ЛР2-Т1 | design.md §7 | Ручной расчёт A=50 совпадает с контролем |
| ЛР3-Ф1 | ForecastService.Forecast; PlanValidator | Все входы проверены, initialDebt=0, перенос C/D |
| ЛР3-Ф2 | FinancialCalculator.CalculateMonth/Summarize | Месячные и итоговые показатели, максимум дефицитов |
| ЛР3-Ф3 | ComparisonService.Compare | Два независимых результата без изменения входов |
| ЛР3-А1 | Domain/FinancialCalculator; Application/ForecastService; Console/Program | Формулы, цикл, форматирование разделены |
| ЛР3-Т1 | ForecastTests.AcceptanceExamples_MatchLab1ExpectedFile; VariedMonths_* | Автоматизированы вычислимые примеры ЛР1 и разные месяцы |
| ЛР3-Т2 | NegativeCash_*; RepeatedForecast_*; EditingOneMonth_*; ComparisonTests | Перенос, отрицательный C, повторы, неизменность |
| ЛР3-Т3 | ValidationTests | Отрицательный initialCash, пустое имя, неверные месяцы отклоняются |
| ЛР3-Д1 | requirements.md §6 | Имена операций/тестов; ещё не реализованные БД/UI указаны явно |

Все файлы — единый комплект. Документы и исходники схем остаются редактируемыми. Фактические результаты — validation.md; наличие таблицы не подменяет запуск тестов.
