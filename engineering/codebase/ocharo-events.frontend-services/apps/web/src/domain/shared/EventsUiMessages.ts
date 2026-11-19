import type { UiMessageTable } from './UiMessages';

/** Events: the page around the fe-events kit, Ocharo's labels the kit shows, the past events to close and the Telegram delivery. */
export const eventsUiMessages: UiMessageTable = {
  Events: { uz: 'Tadbirlar', ru: 'Мероприятия' },
  'Showcase events': { uz: 'Namoyish tadbirlari', ru: 'Мероприятия для показа' },
  "Every event where Ocharo could show its products — local, regional and international — with dates, deadlines, costs and where Ocharo stands. Events records decisions; it never applies, registers or pays on Ocharo's behalf.":
    {
      uz: 'Ocharo o‘z mahsulotlarini namoyish eta oladigan barcha tadbirlar — mahalliy, mintaqaviy va xalqaro — sanalar, muddatlar, narxlar va Ocharo qaysi bosqichda ekani bilan. Events faqat qarorlarni qayd etadi; Ocharo nomidan hech qachon ariza bermaydi, ro‘yxatdan o‘tmaydi va to‘lov qilmaydi.',
      ru: 'Все мероприятия, где Ocharo может показать свою продукцию, — местные, региональные и международные — с датами, сроками, стоимостью и текущим статусом Ocharo. Events только фиксирует решения и никогда не подаёт заявки, не регистрируется и не платит от имени Ocharo.',
    },
  'All events': { uz: 'Barcha tadbirlar', ru: 'Все мероприятия' },

  "Ocharo's cost": { uz: 'Ocharo xarajati', ru: 'Расходы Ocharo' },

  // The kit's Ocharo fields and the past events to close (OE12)
  'Source link': { uz: 'Manba havolasi', ru: 'Ссылка на источник' },
  'Past, not closed': { uz: 'O‘tgan, lekin yopilmagan', ru: 'Прошли, но не закрыты' },
  'These events have ended while Ocharo was still pursuing them. Record whether Ocharo attended or skipped each.': {
    uz: 'Bu tadbirlar Ocharo hali ular bilan shug‘ullanayotgan paytda tugadi. Har biri uchun Ocharo qatnashganmi yoki o‘tkazib yuborganmi, belgilang.',
    ru: 'Эти мероприятия закончились, пока Ocharo ещё работала с ними. Отметьте для каждого, участвовала Ocharo или пропустила его.',
  },
  'An event changed since the list was read, so the list was reloaded.': {
    uz: 'Ro‘yxat o‘qilganidan keyin tadbir o‘zgardi, shuning uchun ro‘yxat qayta yuklandi.',
    ru: 'Мероприятие изменилось после загрузки списка, поэтому список обновлён.',
  },

  // Sources freshness under the title (E24)
  'Sources updated {time}': { uz: 'Manbalar {time} yangilandi', ru: 'Источники обновлены {time}' },
  'No new records in over 24 hours.': {
    uz: 'So‘nggi 24 soatdan ortiq vaqt davomida yangi yozuvlar kelmadi.',
    ru: 'Новых записей нет больше 24 часов.',
  },
  'Sources failing: {count}': { uz: 'Xato bergan manbalar: {count}', ru: 'Источников с ошибкой: {count}' },

  // Telegram delivery
  Settings: { uz: 'Sozlamalar', ru: 'Настройки' },
  'Telegram delivery': { uz: 'Telegram orqali yuborish', ru: 'Рассылка в Telegram' },
  "Ocharo's events go out through the Ocharo Telegram bot to the people named here, on the schedule set here. Each event links to its page in Events.":
    {
      uz: 'Ocharo tadbirlari shu yerda ko‘rsatilgan odamlarga Ocharo Telegram boti orqali, shu yerda belgilangan jadval bo‘yicha yuboriladi. Har bir tadbir Events’dagi o‘z sahifasiga havola qiladi.',
      ru: 'Мероприятия Ocharo уходят через Telegram-бота Ocharo людям, указанным здесь, по расписанию, заданному здесь. Каждое мероприятие ссылается на свою страницу в Events.',
    },
  'Recipients default to role:member, every member of the organization at each run; to reach one person only, type their Ocharo account id instead, one per line. Someone who has not linked Telegram to their Ocharo account shows as not_linked in the run history.':
    {
      uz: 'Qabul qiluvchilar odatda role:member, ya’ni har bir ishga tushganda tashkilotning barcha a’zolari; faqat bir kishiga yuborish uchun uning Ocharo hisobi identifikatorini, har birini alohida qatorda yozing. Ocharo hisobiga Telegram ulamagan kishi yuborishlar tarixida not_linked bo‘lib ko‘rinadi.',
      ru: 'По умолчанию получатели: role:member, то есть все участники организации при каждом запуске; чтобы написать одному человеку, укажите вместо этого идентификатор его аккаунта Ocharo, по одному в строке. Тот, кто не привязал Telegram к аккаунту Ocharo, отображается в истории отправок как not_linked.',
    },
  'Deadline reminders': { uz: 'Muddat eslatmalari', ru: 'Напоминания о сроках' },
  'Every day at 09:00 (Tashkent), every member gets the events that are shortlisted, applying or applied and have an application deadline within 7 days. It is added switched off; switch it on in the list below.':
    {
      uz: 'Har kuni soat 09:00 da (Toshkent) har bir a’zoga saralangan, ariza tayyorlanayotgan yoki ariza topshirilgan va ariza muddati 7 kun ichida bo‘lgan tadbirlar yuboriladi. U o‘chirilgan holda qo‘shiladi; uni quyidagi ro‘yxatda yoqing.',
      ru: 'Каждый день в 09:00 (Ташкент) каждый участник получает мероприятия на этапах «В шорт-листе», «Готовим заявку» и «Заявка подана» со сроком подачи в ближайшие 7 дней. Рассылка добавляется выключенной; включите её в списке ниже.',
    },
  'Add deadline reminders': { uz: 'Muddat eslatmalarini qo‘shish', ru: 'Добавить напоминания о сроках' },
  'Your account id: {id}': { uz: 'Hisobingiz identifikatori: {id}', ru: 'Идентификатор вашего аккаунта: {id}' },
  "Only the organization's admins set its Telegram delivery.": {
    uz: 'Telegram orqali yuborishni faqat tashkilot administratorlari sozlaydi.',
    ru: 'Рассылку в Telegram настраивают только администраторы организации.',
  },
  'Cost not stated': { uz: 'Narx ko‘rsatilmagan', ru: 'Стоимость не указана' },

  // Event details
  'How to take part': { uz: 'Qanday qatnashish mumkin', ru: 'Как принять участие' },

  // Scopes, kinds and stages, handed to the fe-events kit
  Local: { uz: 'Mahalliy', ru: 'Местное' },
  Regional: { uz: 'Mintaqaviy', ru: 'Региональное' },
  International: { uz: 'Xalqaro', ru: 'Международное' },
  'Runway show': { uz: 'Podium shousi', ru: 'Показ на подиуме' },
  'Trade fair': { uz: 'Savdo yarmarkasi', ru: 'Торговая ярмарка' },
  Market: { uz: 'Bozor', ru: 'Маркет' },
  Exhibition: { uz: 'Ko‘rgazma', ru: 'Выставка' },
  Competition: { uz: 'Tanlov', ru: 'Конкурс' },
  Festival: { uz: 'Festival', ru: 'Фестиваль' },
  Conference: { uz: 'Konferensiya', ru: 'Конференция' },
  Candidate: { uz: 'Nomzod', ru: 'Кандидат' },
  Shortlisted: { uz: 'Saralangan', ru: 'В шорт-листе' },
  Applying: { uz: 'Ariza tayyorlanmoqda', ru: 'Готовим заявку' },
  Applied: { uz: 'Ariza topshirilgan', ru: 'Заявка подана' },
  Attended: { uz: 'Qatnashilgan', ru: 'Участвовали' },
  Skipped: { uz: 'O‘tkazib yuborilgan', ru: 'Пропущено' },
  Rejected: { uz: 'Rad etilgan', ru: 'Отклонено' },
};
