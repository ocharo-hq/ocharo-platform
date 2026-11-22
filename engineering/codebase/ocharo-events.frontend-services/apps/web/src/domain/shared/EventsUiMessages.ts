import type { UiMessageTable } from './UiMessages';

/** Events: the page around the fe-events kit, Ocharo's labels the kit shows, the past events to close and the Telegram delivery. */
export const eventsUiMessages: UiMessageTable = {
  Events: { uz: 'Tadbirlar', ru: 'Мероприятия' },
  'All events': { uz: 'Barcha tadbirlar', ru: 'Все мероприятия' },

  "Ocharo's cost": { uz: 'Ocharo xarajati', ru: 'Расходы Ocharo' },

  // The kit's Ocharo fields and the past events to close (OE12)
  'Source link': { uz: 'Manba havolasi', ru: 'Ссылка на источник' },
  'Past, not closed': { uz: 'O‘tgan, lekin yopilmagan', ru: 'Прошли, но не закрыты' },
  '{count} past to close': { uz: 'Yopiladigan o‘tganlar: {count}', ru: 'Прошедших к закрытию: {count}' },
  'These events have ended while Ocharo was still pursuing them. Record whether Ocharo attended or skipped each.': {
    uz: 'Bu tadbirlar Ocharo hali ular bilan shug‘ullanayotgan paytda tugadi. Har biri uchun Ocharo qatnashganmi yoki o‘tkazib yuborganmi, belgilang.',
    ru: 'Эти мероприятия закончились, пока Ocharo ещё работала с ними. Отметьте для каждого, участвовала Ocharo или пропустила его.',
  },
  'An event changed since the list was read, so the list was reloaded.': {
    uz: 'Ro‘yxat o‘qilganidan keyin tadbir o‘zgardi, shuning uchun ro‘yxat qayta yuklandi.',
    ru: 'Мероприятие изменилось после загрузки списка, поэтому список обновлён.',
  },

  // Reminders
  'Reminder settings are on their way': {
    uz: 'Eslatma sozlamalari tayyorlanmoqda',
    ru: 'Настройки напоминаний готовятся',
  },
  'Choose which events remind whom, on which channel and when. This page fills in with the reminder system.': {
    uz: 'Qaysi tadbirlar kimga, qaysi kanal orqali va qachon eslatilishini tanlang. Bu sahifa eslatmalar tizimi bilan to‘ladi.',
    ru: 'Выберите, какие события, кому, по какому каналу и когда напоминать. Страница заполнится вместе с системой напоминаний.',
  },

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
  'Your account id: {id}': { uz: 'Hisobingiz identifikatori: {id}', ru: 'Идентификатор вашего аккаунта: {id}' },
  "Only the organization's admins set its Telegram delivery.": {
    uz: 'Telegram orqali yuborishni faqat tashkilot administratorlari sozlaydi.',
    ru: 'Рассылку в Telegram настраивают только администраторы организации.',
  },

  // Event details
  'How to take part': { uz: 'Qanday qatnashish mumkin', ru: 'Как принять участие' },

  // Scopes, kinds and stages, handed to the fe-events kit
  National: { uz: 'Milliy', ru: 'Национальное' },
  'Central Asia': { uz: 'Markaziy Osiyo', ru: 'Центральная Азия' },
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
