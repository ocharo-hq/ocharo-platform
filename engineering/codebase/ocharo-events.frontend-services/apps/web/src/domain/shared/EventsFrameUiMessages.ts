import type { UiMessageTable } from './UiMessages';

/**
 * The Ocharo frame in Events: the app menu, the bar, the account menu and the SDK copy the frame shows. The suite's
 * wording, as Desk, Catalogue and Brand carry it. The page's own phrases live in `EventsUiMessages.ts`.
 */
export const eventsFrameUiMessages: UiMessageTable = {
  // The app menu.
  Desk: { uz: 'Desk', ru: 'Desk' },
  Catalogue: { uz: 'Katalog', ru: 'Каталог' },
  Studio: { uz: 'Studiya', ru: 'Студия' },
  Marketing: { uz: 'Marketing', ru: 'Маркетинг' },
  'Apps menu: Editorial': { en: 'Editorial', uz: 'Tahririyat', ru: 'Редакция' },
  Brand: { uz: 'Brend', ru: 'Бренд' },
  Atlas: { uz: 'Atlas', ru: 'Atlas' },
  Wear: { uz: 'Wear', ru: 'Wear' },
  Blog: { uz: 'Blog', ru: 'Блог' },
  'Trade events and deadlines': { uz: 'Savdo tadbirlari va muddatlar', ru: 'Торговые мероприятия и сроки' },
  'Events, sourcing and products': { uz: 'Tadbirlar, ta’minot va mahsulotlar', ru: 'События, снабжение и товары' },
  'Products, prices and editions': { uz: 'Mahsulotlar, narxlar va nashrlar', ru: 'Товары, цены и издания' },
  'Garments and fabric': { uz: 'Kiyimlar va matolar', ru: 'Одежда и ткани' },
  'Posts and leads': { uz: 'Postlar va murojaatlar', ru: 'Публикации и заявки' },
  'Blog stories': { uz: 'Blog hikoyalari', ru: 'Истории блога' },
  'Logo and brand guide': { uz: 'Logotip va brend qo‘llanmasi', ru: 'Логотип и брендбук' },
  'Public site': { uz: 'Ommaviy sayt', ru: 'Публичный сайт' },
  'Public sites': { uz: 'Ommaviy saytlar', ru: 'Публичные сайты' },
  'Ocharo apps': { uz: 'Ocharo ilovalari', ru: 'Приложения Ocharo' },
  '(opens in a new tab)': { uz: '(yangi oynada ochiladi)', ru: '(откроется в новой вкладке)' },
  'Ocharo apps — {app}': { uz: 'Ocharo ilovalari — {app}', ru: 'Приложения Ocharo — {app}' },
  'Ocharo app links could not be loaded, so the app menu uses the built-in addresses.': {
    uz: 'Ocharo ilovalari havolalarini yuklab bo‘lmadi, shuning uchun ilovalar menyusi o‘rnatilgan manzillardan foydalanadi.',
    ru: 'Не удалось загрузить ссылки на приложения Ocharo, поэтому меню приложений использует встроенные адреса.',
  },
  'Some Ocharo app links are misconfigured, so the app menu leaves them out.': {
    uz: 'Ba’zi Ocharo ilovalari havolalari noto‘g‘ri sozlangan, shuning uchun ilovalar menyusida ko‘rsatilmaydi.',
    ru: 'Некоторые ссылки на приложения Ocharo настроены неверно, поэтому меню приложений их не показывает.',
  },

  // The bar.
  'Version {version}': { uz: 'Versiya {version}', ru: 'Версия {version}' },
  'Version {running} is live; this tab runs {version}. Reload to update.': {
    uz: '{running} versiyasi chiqdi; bu oyna {version} versiyasida ishlayapti. Yangilash uchun sahifani qayta yuklang.',
    ru: 'Вышла версия {running}; эта вкладка работает на {version}. Перезагрузите страницу, чтобы обновить.',
  },
  'You are here': { uz: 'Siz shu yerdasiz', ru: 'Вы здесь' },

  // The places' titles and the frame's navigation copy.
  'Page not found': { uz: 'Sahifa topilmadi', ru: 'Страница не найдена' },
  'Page not found.': { uz: 'Sahifa topilmadi.', ru: 'Страница не найдена.' },
  'Back to events': { uz: 'Tadbirlarga qaytish', ru: 'Вернуться к мероприятиям' },
  'Expand sidebar': { uz: 'Yon panelni yoyish', ru: 'Развернуть боковую панель' },
  'Collapse sidebar': { uz: 'Yon panelni yig‘ish', ru: 'Свернуть боковую панель' },
  'Open navigation': { uz: 'Navigatsiyani ochish', ru: 'Открыть навигацию' },
  Navigation: { uz: 'Navigatsiya', ru: 'Навигация' },

  // The account menu and its versions.
  Account: { uz: 'Hisob', ru: 'Аккаунт' },
  'Account menu for {name}': { uz: '{name} hisob menyusi', ru: 'Меню учётной записи {name}' },
  'Account menu': { uz: 'Hisob menyusi', ru: 'Меню учётной записи' },
  Appearance: { uz: 'Ko‘rinish', ru: 'Оформление' },
  System: { uz: 'Tizim', ru: 'Система' },
  Light: { uz: 'Yorug‘', ru: 'Светлая' },
  Dark: { uz: 'Qorong‘i', ru: 'Тёмная' },
  'Interface language': { uz: 'Interfeys tili', ru: 'Язык интерфейса' },
  Organization: { uz: 'Tashkilot', ru: 'Организация' },
  Versions: { uz: 'Versiyalar', ru: 'Версии' },
  'Sign out': { uz: 'Chiqish', ru: 'Выйти' },
  'Signed in to Ocharo': { uz: 'Ocharo hisobiga kirilgan', ru: 'Вход выполнен в Ocharo' },
  'Could not sign out. Try again.': {
    uz: 'Hisobdan chiqib bo‘lmadi. Qayta urinib ko‘ring.',
    ru: 'Не удалось выйти. Попробуйте ещё раз.',
  },
  'Not checked': { uz: 'Tekshirilmagan', ru: 'Не проверено' },
  'Checking…': { uz: 'Tekshirilmoqda…', ru: 'Проверка…' },
  Unavailable: { uz: 'Mavjud emas', ru: 'Недоступно' },

  // The Ocharo session banner.
  'This Ocharo account has no access to Events. Sign out and sign in with an Ocharo account.': {
    uz: 'Bu hisob Ocharo Events’ga kira olmaydi. Chiqing va Ocharo hisobi bilan kiring.',
    ru: 'У этой учётной записи нет доступа к Events. Выйдите и войдите с учётной записью Ocharo.',
  },
  'You have read-only access.': { uz: 'Sizda faqat ko‘rish ruxsati bor.', ru: 'У вас доступ только для чтения.' },
  'Reconnect to Ocharo. Your unsaved changes stay in this tab.': {
    uz: 'Ocharo hisobiga qayta ulaning. Saqlanmagan o‘zgarishlar shu oynada qoladi.',
    ru: 'Подключитесь к Ocharo снова. Несохранённые изменения останутся в этой вкладке.',
  },
  'Sign in to Ocharo': { uz: 'Ocharo hisobiga kirish', ru: 'Войти в Ocharo' },
  'Sign-in was not completed. Try again.': {
    uz: 'Kirish yakunlanmadi. Qayta urinib ko‘ring.',
    ru: 'Вход не завершён. Попробуйте ещё раз.',
  },
  'Check connection': { uz: 'Ulanishni tekshirish', ru: 'Проверить подключение' },

  // Feedback and the SDK copy inside the frame.
  'Could not complete the request': { uz: 'So‘rovni bajarib bo‘lmadi', ru: 'Не удалось выполнить запрос' },
  'Could not open this page': { uz: 'Bu bo‘limni ochib bo‘lmadi.', ru: 'Не удалось открыть страницу' },
  Retry: { uz: 'Qayta urinish', ru: 'Повторить' },
  Close: { uz: 'Yopish', ru: 'Закрыть' },
  'Skip to content': { uz: 'Asosiy bo‘limga o‘tish', ru: 'Перейти к содержимому' },
  'Search…': { uz: 'Qidirish…', ru: 'Поиск…' },
  'No results': { uz: 'Natija yo‘q', ru: 'Нет результатов' },
  'Delete notification': { uz: 'Bildirishnomani o‘chirish', ru: 'Удалить уведомление' },
  Notifications: { uz: 'Bildirishnomalar', ru: 'Уведомления' },
  'Close notifications': { uz: 'Bildirishnomalarni yopish', ru: 'Закрыть уведомления' },
  Remove: { uz: 'Olib tashlash', ru: 'Удалить' },
  'Loading…': { uz: 'Yuklanmoqda…', ru: 'Загрузка…' },
};
