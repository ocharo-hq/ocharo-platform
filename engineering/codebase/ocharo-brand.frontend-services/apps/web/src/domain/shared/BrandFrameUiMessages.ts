import type { UiMessageTable } from './UiMessages';

/**
 * The Ocharo frame in Brand: the app menu, the sections, the phone bar, the places' titles, the account menu and the
 * SDK copy the frame shows. The suite's wording, as Desk and Catalogue carry it; Brand's own section names keep the
 * Uzbek words its sidebar already used.
 */
export const brandFrameUiMessages: UiMessageTable = {
  // The app menu.
  Desk: { uz: 'Desk', ru: 'Desk' },
  Catalogue: { uz: 'Katalog', ru: 'Каталог' },
  Studio: { uz: 'Studiya', ru: 'Студия' },
  Marketing: { uz: 'Marketing', ru: 'Маркетинг' },
  Editorial: { uz: 'Jurnal uslubi', ru: 'Журнальный стиль' },
  Brand: { uz: 'Brend', ru: 'Бренд' },
  Atlas: { uz: 'Atlas', ru: 'Atlas' },
  Wear: { uz: 'Wear', ru: 'Wear' },
  Blog: { uz: 'Blog', ru: 'Блог' },
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
  'App version': { uz: 'Ilova versiyasi', ru: 'Версия приложения' },
  'You are here': { uz: 'Siz shu yerdasiz', ru: 'Вы здесь' },
  'Release {release} · {count} unpublished changes': {
    uz: '{release}-nashr · {count} ta nashr qilinmagan o‘zgarish',
    ru: 'Выпуск {release} · неопубликованных изменений: {count}',
  },
  'Not released yet': { uz: 'Hali nashr qilinmagan', ru: 'Ещё не выпущено' },
  'Checking the release…': { uz: 'Nashr tekshirilmoqda…', ru: 'Проверка выпуска…' },
  Refresh: { uz: 'Yangilash', ru: 'Обновить' },

  // The sections and the places' titles.
  'Brand sections': { uz: 'Brend bo‘limlari', ru: 'Разделы бренда' },
  // Brand's sidebar said Umumiy; the suite's longer Umumiy ko‘rinish wraps in the phone bar.
  Overview: { uz: 'Umumiy', ru: 'Обзор' },
  Identity: { uz: 'Identitet', ru: 'Айдентика' },
  Logos: { uz: 'Logotiplar', ru: 'Логотипы' },
  Appearances: { uz: 'Tadbirlar', ru: 'События' },
  Connect: { uz: 'Ulanish', ru: 'Подключение' },
  Admin: { uz: 'Boshqaruv', ru: 'Администрирование' },
  'Expand sidebar': { uz: 'Yon panelni yoyish', ru: 'Развернуть боковую панель' },
  'Collapse sidebar': { uz: 'Yon panelni yig‘ish', ru: 'Свернуть боковую панель' },
  More: { uz: 'Yana', ru: 'Ещё' },
  'More in Brand': { uz: 'Brenddagi boshqa bo‘limlar', ru: 'Другие разделы бренда' },

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
  'Signed in through Ocharo Desk': { uz: 'Ocharo Desk orqali kirilgan', ru: 'Вход выполнен через Ocharo Desk' },
  'Could not sign out. Try again.': {
    uz: 'Hisobdan chiqib bo‘lmadi. Qayta urinib ko‘ring.',
    ru: 'Не удалось выйти. Попробуйте ещё раз.',
  },
  'Not checked': { uz: 'Tekshirilmagan', ru: 'Не проверено' },
  'Checking…': { uz: 'Tekshirilmoqda…', ru: 'Проверка…' },
  Unavailable: { uz: 'Mavjud emas', ru: 'Недоступно' },

  // The Ocharo session banner.
  'Reconnect to Ocharo. Your unsaved changes stay in this tab.': {
    uz: 'Ocharo hisobiga qayta ulaning. Saqlanmagan o‘zgarishlar shu oynada qoladi.',
    ru: 'Подключитесь к Ocharo снова. Несохранённые изменения останутся в этой вкладке.',
  },
  'Sign in to Ocharo': { uz: 'Ocharo hisobiga kirish', ru: 'Войти в Ocharo' },
  'Check connection': { uz: 'Ulanishni tekshirish', ru: 'Проверить подключение' },
  'You have read-only access.': { uz: 'Sizda faqat ko‘rish ruxsati bor.', ru: 'У вас доступ только для чтения.' },

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
