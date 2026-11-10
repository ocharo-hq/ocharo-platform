import type { UiMessageTable } from './UiMessages';

/** The phrases Events shares with every Ocharo app's pages: failures, confirmations, form words and the labels the events domain keeps. */
export const eventsCommonUiMessages: UiMessageTable = {
  // Failures, in the words the suite's pages use.
  'Check the entered values and try again.': {
    uz: 'Kiritilgan ma’lumotlarni tekshirib qayta urinib ko‘ring.',
    ru: 'Проверьте введённые значения и повторите.',
  },
  'Your session is no longer valid. Sign in again.': {
    uz: 'Sessiya muddati tugagan. Qayta kiring.',
    ru: 'Сеанс недействителен. Войдите снова.',
  },
  'This action is not permitted, or your session changed. Reload to check your access.': {
    uz: 'Amalga ruxsat yo‘q yoki sessiya o‘zgardi. Kirish huquqini tekshirish uchun qayta yuklang.',
    ru: 'Действие запрещено или сеанс изменился. Перезагрузите для проверки доступа.',
  },
  'This record is unavailable in this organization.': {
    uz: 'Bu yozuv ushbu tashkilotda mavjud emas.',
    ru: 'Эта запись недоступна в данной организации.',
  },
  'This record changed. Your edits are still here. Reload the latest version before trying again.': {
    uz: 'Yozuv o‘zgardi. Tahrirlaringiz saqlanib turibdi. Qayta urinishdan oldin oxirgi versiyani yuklang.',
    ru: 'Запись изменилась. Ваши правки сохранены. Загрузите последнюю версию перед повтором.',
  },
  'This record changed. Reload the latest version before trying again.': {
    uz: 'Bu yozuv o‘zgargan. Qayta urinishdan oldin so‘nggi versiyani yuklang.',
    ru: 'Эта запись изменилась. Загрузите последнюю версию, прежде чем повторить попытку.',
  },
  'The service returned an unexpected response. Please retry.': {
    uz: 'Xizmat kutilmagan javob qaytardi. Qayta urinib ko‘ring.',
    ru: 'Служба вернула неожиданный ответ. Повторите попытку.',
  },
  'The request was cancelled. Your edits have been retained.': {
    uz: 'So‘rov bekor qilindi. Tahrirlaringiz saqlanib turibdi.',
    ru: 'Запрос отменён. Ваши правки сохранены.',
  },
  'The service could not complete this request. Your edits are still here; try again.': {
    uz: 'Xizmat so‘rovni bajara olmadi. Tahrirlaringiz saqlanib turibdi; qayta urinib ko‘ring.',
    ru: 'Служба не выполнила запрос. Ваши правки сохранены; повторите попытку.',
  },
  'Saved successfully. The current view could not refresh. Your local edits are preserved.': {
    uz: 'Saqlandi. Ko‘rinishni yangilab bo‘lmadi. Mahalliy o‘zgarishlaringiz saqlanib qoldi.',
    ru: 'Сохранено. Не удалось обновить представление. Ваши локальные изменения сохранены.',
  },
  'Content unavailable. Try refreshing.': {
    uz: 'Ma’lumotlar mavjud emas. Yangilab ko‘ring.',
    ru: 'Данные недоступны. Попробуйте обновить.',
  },

  // Confirmations and form words.
  'Discard unsaved changes and leave this page?': {
    uz: 'Saqlanmagan o‘zgarishlardan voz kechib, sahifadan chiqasizmi?',
    ru: 'Отменить несохранённые изменения и покинуть страницу?',
  },
  'Discard changes': { uz: 'O‘zgarishlarni bekor qilish', ru: 'Отменить изменения' },
  'Discard my edits and reload': {
    uz: 'O‘zgarishlarimni bekor qilib, qayta yuklash',
    ru: 'Отменить мои правки и перезагрузить',
  },
  'Reload latest': { uz: 'So‘nggi versiyani yuklash', ru: 'Загрузить последнюю версию' },
  Cancel: { uz: 'Bekor qilish', ru: 'Отмена' },
  Refresh: { uz: 'Yangilash', ru: 'Обновить' },
  Status: { uz: 'Holat', ru: 'Статус' },
  Notes: { uz: 'Izohlar', ru: 'Заметки' },
  Website: { uz: 'Veb-sayt', ru: 'Сайт' },
  City: { uz: 'Shahar', ru: 'Город' },
  'Country code': { uz: 'Mamlakat kodi', ru: 'Код страны' },
  'Edit details': { uz: 'Ma’lumotlarni tahrirlash', ru: 'Изменить сведения' },
  Other: { uz: 'Boshqa', ru: 'Другое' },
  Confirmed: { uz: 'Tasdiqlangan', ru: 'Подтверждено' },

  // Form checks the events domain raises.
  'City is limited to 120 characters.': {
    uz: 'Shahar nomi 120 belgi bilan cheklangan.',
    ru: 'Название города ограничено 120 символами.',
  },
  'Enter the website as a full http:// or https:// address.': {
    uz: 'Veb-saytni to‘liq http:// yoki https:// manzil sifatida kiriting.',
    ru: 'Укажите сайт полным адресом http:// или https://.',
  },
  'Enter the source as a full http:// or https:// address.': {
    uz: 'Manbani to‘liq http:// yoki https:// manzil sifatida kiriting.',
    ru: 'Укажите источник полным адресом http:// или https://.',
  },
  'Notes are limited to 8000 characters.': {
    uz: 'Izohlar 8000 belgi bilan cheklangan.',
    ru: 'Заметки ограничены 8000 символами.',
  },
};
