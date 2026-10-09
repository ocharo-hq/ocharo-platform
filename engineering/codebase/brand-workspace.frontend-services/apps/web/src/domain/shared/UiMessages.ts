import { sourcingViewUiMessages } from './SourcingViewUiMessages';
import { integrationUiMessages } from './IntegrationUiMessages';
import { catalogueUiMessages } from './CatalogueUiMessages';
import { productReviewUiMessages } from './ProductReviewUiMessages';
import { designerUiMessages } from './DesignerUiMessages';
import { eventsUiMessages } from './EventsUiMessages';
import { sourcingUiMessages } from './SourcingUiMessages';

/** Operator interface locales are independent from authored catalogue languages. */
export type UiLocale = 'uz' | 'ru' | 'en';
export type UiMessageTable = Record<string, { uz: string; ru: string }>;

/** English source phrases identify operator messages; authored content never passes through this table. */
const baseUiMessages: UiMessageTable = {
  'Loading…': { uz: 'Yuklanmoqda…', ru: 'Загрузка…' },
  'Search…': { uz: 'Qidirish…', ru: 'Поиск…' },
  'No results': { uz: 'Natija yo‘q', ru: 'Нет результатов' },
  'Refresh': {uz:'Yangilash',ru:'Обновить'},
  'Content unavailable. Try refreshing.': {uz:'Ma’lumotlar mavjud emas. Yangilab ko‘ring.',ru:'Данные недоступны. Попробуйте обновить.'},
  'Saved products unavailable': {uz:'Saqlangan mahsulotlar mavjud emas',ru:'Сохранённые товары недоступны'},
  'The service could not be reached. Your edits have been retained.': {uz:'Xizmatga ulanib bo‘lmadi. O‘zgarishlaringiz saqlanib qoldi.',ru:'Не удалось подключиться к сервису. Ваши изменения сохранены.'},

  'Catalogues unavailable': { uz: 'Kataloglar mavjud emas', ru: 'Каталоги недоступны' },
  'Saved successfully. The current view could not refresh. Your local edits are preserved.': { uz: 'Saqlandi. Ko‘rinishni yangilab bo‘lmadi. Mahalliy o‘zgarishlaringiz saqlanib qoldi.', ru: 'Сохранено. Не удалось обновить представление. Ваши локальные изменения сохранены.' },
  'Interface language': { uz: 'Interfeys tili', ru: 'Язык интерфейса' },
  'Ocharo workspace': { uz: 'Ocharo ish maydoni', ru: 'Рабочее пространство Ocharo' },
  'Your creative business': { uz: 'Sizning ijodiy biznesingiz', ru: 'Ваш творческий бизнес' },
  'Sign out': { uz: 'Chiqish', ru: 'Выйти' },
  'Sign in': { uz: 'Kirish', ru: 'Войти' },
  'Organization': { uz: 'Tashkilot', ru: 'Организация' },
  'All organizations': { uz: 'Barcha tashkilotlar', ru: 'Все организации' },
  'Workspace sections': { uz: 'Ish maydoni bo‘limlari', ru: 'Разделы рабочего пространства' },
  'Overview': { uz: 'Umumiy ko‘rinish', ru: 'Обзор' },
  'Products & releases': { uz: 'Mahsulotlar va nashrlar', ru: 'Товары и публикации' },
  'Connected apps': { uz: 'Ulangan ilovalar', ru: 'Подключённые приложения' },
  'Catalogue designer': { uz: 'Katalog muharriri', ru: 'Редактор каталога' },
  'Business cards': { uz: 'Tashrifnomalar', ru: 'Визитки' },
  'Content': { uz: 'Kontent', ru: 'Контент' },
  'Media': { uz: 'Media', ru: 'Медиа' },
  'Brands': { uz: 'Brendlar', ru: 'Бренды' },
  'System': { uz: 'Tizim', ru: 'Система' },
  'Loading your workspace…': { uz: 'Ish maydoni yuklanmoqda…', ru: 'Загрузка рабочего пространства…' },
  'Workspace unavailable': { uz: 'Ish maydoni mavjud emas', ru: 'Рабочее пространство недоступно' },
  'Organization unavailable': { uz: 'Tashkilot mavjud emas', ru: 'Организация недоступна' },
  'Choose one of your current memberships.': { uz: 'A’zo bo‘lgan tashkilotingizni tanlang.', ru: 'Выберите организацию, в которой вы состоите.' },
  'Reload workspace': { uz: 'Ish maydonini qayta yuklash', ru: 'Перезагрузить рабочее пространство' },
  'Save': { uz: 'Saqlash', ru: 'Сохранить' },
  'Cancel': { uz: 'Bekor qilish', ru: 'Отмена' },
  'Close': { uz: 'Yopish', ru: 'Закрыть' },
  'Delete': { uz: 'O‘chirish', ru: 'Удалить' },
  'Remove': { uz: 'Olib tashlash', ru: 'Убрать' },
  'Add': { uz: 'Qo‘shish', ru: 'Добавить' },
  'Edit': { uz: 'Tahrirlash', ru: 'Изменить' },
  'Name': { uz: 'Nomi', ru: 'Название' },
  'Description': { uz: 'Tavsif', ru: 'Описание' },
  'Price': { uz: 'Narx', ru: 'Цена' },
  'Products': { uz: 'Mahsulotlar', ru: 'Товары' },
  'Catalogue': { uz: 'Katalog', ru: 'Каталог' },
  'Language': { uz: 'Til', ru: 'Язык' },
};

export const operatorUiMessages: UiMessageTable = {
  "App": {
    "uz": "Ilova",
    "ru": "Приложение"
  },
  "Backend": {
    "uz": "Server",
    "ru": "Сервер"
  },
  "Secondary diagnostics": {
    "uz": "Qo‘shimcha diagnostika",
    "ru": "Дополнительная диагностика"
  },
  "Foundation status": {
    "uz": "Tizim holati",
    "ru": "Состояние системы"
  },
  "Check status": {
    "uz": "Holatni tekshirish",
    "ru": "Проверить состояние"
  },
  "Checking the foundation…": {
    "uz": "Tizim tekshirilmoqda…",
    "ru": "Проверка системы…"
  },
  "Reading the API and worker heartbeat.": {
    "uz": "API va fon xizmati signali tekshirilmoqda.",
    "ru": "Проверяем API и сигнал фоновой службы."
  },
  "Status unavailable": {
    "uz": "Holat mavjud emas",
    "ru": "Состояние недоступно"
  },
  "A previous snapshot exists, but it does not confirm the current state.": {
    "uz": "Oldingi holat yozuvi mavjud, lekin hozirgi holatni tasdiqlamaydi.",
    "ru": "Есть прежний снимок, но он не подтверждает текущее состояние."
  },
  "Foundation API": {
    "uz": "Asosiy API",
    "ru": "Основной API"
  },
  "Reachable": {
    "uz": "Ulanish mavjud",
    "ru": "Доступен"
  },
  "Configured datastore": {
    "uz": "Sozlangan ma’lumotlar bazasi",
    "ru": "Настроенная база данных"
  },
  "Worker heartbeat": {
    "uz": "Fon xizmati signali",
    "ru": "Сигнал фоновой службы"
  },
  "Last heartbeat · local time": {
    "uz": "Oxirgi signal · mahalliy vaqt",
    "ru": "Последний сигнал · местное время"
  },
  "This is a service snapshot. A healthy worker does not establish publication. No publishing actions are available here.": {
    "uz": "Bu xizmat holatining yozuvi. Ishlayotgan fon xizmati nashrni tasdiqlamaydi. Bu yerda nashr qilish amallari yo‘q.",
    "ru": "Это снимок состояния службы. Работа фоновой службы не подтверждает публикацию. Здесь нет действий публикации."
  },
  "Organization configuration": {
    "uz": "Tashkilot sozlamalari",
    "ru": "Настройки организации"
  },
  "Existing brands": {
    "uz": "Mavjud brendlar",
    "ru": "Существующие бренды"
  },
  "No brands yet. An owner can create the first brand.": {
    "uz": "Hali brendlar yo‘q. Egasi birinchi brendni yaratishi mumkin.",
    "ru": "Брендов пока нет. Владелец может создать первый."
  },
  "Create a brand": {
    "uz": "Brend yaratish",
    "ru": "Создать бренд"
  },
  "Brand name": {
    "uz": "Brend nomi",
    "ru": "Название бренда"
  },
  "Time zone": {
    "uz": "Vaqt mintaqasi",
    "ru": "Часовой пояс"
  },
  "For example Asia/Tashkent or UTC.": {
    "uz": "Masalan, Asia/Tashkent yoki UTC.",
    "ru": "Например, Asia/Tashkent или UTC."
  },
  "Create brand": {
    "uz": "Brend yaratish",
    "ru": "Создать бренд"
  },
  "Brand authority": {
    "uz": "Brend boshqaruvi",
    "ru": "Управление брендом"
  },
  "Manage your products, identity and the catalogue your customers see.": {
    "uz": "Mahsulotlar, brend qiyofasi va mijozlar ko‘radigan katalogni boshqaring.",
    "ru": "Управляйте товарами, оформлением бренда и каталогом для покупателей."
  },
  "Brand": {
    "uz": "Brend",
    "ru": "Бренд"
  },
  "Choose a brand": {
    "uz": "Brendni tanlang",
    "ru": "Выберите бренд"
  },
  "Create a brand in Brands to begin.": {
    "uz": "Boshlash uchun Brendlar bo‘limida brend yarating.",
    "ru": "Для начала создайте бренд в разделе «Бренды»."
  },
  "Loading brand records…": {
    "uz": "Brend ma’lumotlari yuklanmoqda…",
    "ru": "Загрузка данных бренда…"
  },
  "Start this brand’s product library": {
    "uz": "Brend mahsulotlari kutubxonasini boshlash",
    "ru": "Создать библиотеку товаров бренда"
  },
  "Records remain private until an owner publishes a release.": {
    "uz": "Egasi nashr qilmaguncha yozuvlar yopiq qoladi.",
    "ru": "Записи остаются закрытыми до публикации владельцем."
  },
  "Start an empty draft": {
    "uz": "Bo‘sh qoralama yaratish",
    "ru": "Создать пустой черновик"
  },
  "Adopt preserved Ocharo records": {
    "uz": "Saqlangan Ocharo yozuvlarini import qilish",
    "ru": "Импортировать сохранённые записи Ocharo"
  },
  "Adoption preserves original product IDs, copy, prices, collections and image hashes. Every image starts awaiting publication review.": {
    "uz": "Import asl mahsulot IDlari, matnlar, narxlar, kolleksiyalar va rasm xeshlarini saqlaydi. Har bir rasm nashrdan oldin ko‘rib chiqiladi.",
    "ru": "Импорт сохраняет исходные ID товаров, тексты, цены, коллекции и хеши изображений. Каждое изображение ожидает проверки перед публикацией."
  },
  "products ·": {
    "uz": "mahsulot ·",
    "ru": "товаров ·"
  },
  "collections · Draft": {
    "uz": "kolleksiya · Qoralama",
    "ru": "коллекций · Черновик"
  },
  "· Unsaved changes": {
    "uz": "· Saqlanmagan o‘zgarishlar",
    "ru": "· Несохранённые изменения"
  },
  "Save draft": {
    "uz": "Qoralamani saqlash",
    "ru": "Сохранить черновик"
  },
  "Brand review": {
    "uz": "Brendni ko‘rib chiqish",
    "ru": "Проверка бренда"
  },
  "New product ID": {
    "uz": "Yangi mahsulot IDsi",
    "ru": "ID нового товара"
  },
  "e.g. nimcha-v08": {
    "uz": "masalan, nimcha-v08",
    "ru": "например, nimcha-v08"
  },
  "Add product": {
    "uz": "Mahsulot qo‘shish",
    "ru": "Добавить товар"
  },
  "Stable ID · Product revision": {
    "uz": "Doimiy ID · Mahsulot versiyasi",
    "ru": "Постоянный ID · Версия товара"
  },
  "· Imported source revision": {
    "uz": "· Import qilingan manba versiyasi",
    "ru": "· Импортированная версия источника"
  },
  "Image from media library": {
    "uz": "Media kutubxonasidan rasm",
    "ru": "Изображение из медиатеки"
  },
  "Choose an uploaded image": {
    "uz": "Yuklangan rasmni tanlang",
    "ru": "Выберите загруженное изображение"
  },
  "Attach image": {
    "uz": "Rasm biriktirish",
    "ru": "Прикрепить изображение"
  },
  "Private notes": {
    "uz": "Ichki izohlar",
    "ru": "Внутренние заметки"
  },
  "Review questions": {
    "uz": "Tekshiruv savollari",
    "ru": "Вопросы для проверки"
  },
  "Products with open questions are excluded from public releases.": {
    "uz": "Ochiq savollari bor mahsulotlar ommaviy nashrga kiritilmaydi.",
    "ru": "Товары с нерешёнными вопросами не входят в публичные публикации."
  },
  "Select a product or add its stable ID.": {
    "uz": "Mahsulotni tanlang yoki doimiy IDsini qo‘shing.",
    "ru": "Выберите товар или добавьте его постоянный ID."
  },
  "Public slug": {
    "uz": "Ommaviy manzil nomi",
    "ru": "Имя публичного адреса"
  },
  "Fixed after first publication.": {
    "uz": "Birinchi nashrdan keyin o‘zgarmaydi.",
    "ru": "Не меняется после первой публикации."
  },
  "tagline": {
    "uz": "shior",
    "ru": "слоган"
  },
  "description": {
    "uz": "tavsif",
    "ru": "описание"
  },
  "Palette": {
    "uz": "Ranglar palitrasi",
    "ru": "Палитра"
  },
  "Color name": {
    "uz": "Rang nomi",
    "ru": "Название цвета"
  },
  "Color role": {
    "uz": "Rang vazifasi",
    "ru": "Назначение цвета"
  },
  "Add color": {
    "uz": "Rang qo‘shish",
    "ru": "Добавить цвет"
  },
  "Official destinations": {
    "uz": "Rasmiy manzillar",
    "ru": "Официальные адреса"
  },
  "Link label": {
    "uz": "Havola nomi",
    "ru": "Название ссылки"
  },
  "HTTPS destination": {
    "uz": "HTTPS manzil",
    "ru": "HTTPS-адрес"
  },
  "Verified official destination": {
    "uz": "Tasdiqlangan rasmiy manzil",
    "ru": "Подтверждённый официальный адрес"
  },
  "Add destination": {
    "uz": "Manzil qo‘shish",
    "ru": "Добавить адрес"
  },
  "Brand logos": {
    "uz": "Brend logotiplari",
    "ru": "Логотипы бренда"
  },
  "Logo slot": {
    "uz": "Logotip joyi",
    "ru": "Место логотипа"
  },
  "Uploaded logo": {
    "uz": "Yuklangan logotip",
    "ru": "Загруженный логотип"
  },
  "Choose image": {
    "uz": "Rasmni tanlang",
    "ru": "Выберите изображение"
  },
  "Assign logo": {
    "uz": "Logotipni belgilash",
    "ru": "Назначить логотип"
  },
  "Add collection": {
    "uz": "Kolleksiya qo‘shish",
    "ru": "Добавить коллекцию"
  },
  "name": {
    "uz": "nomi",
    "ru": "название"
  },
  "Include collection in the next release": {
    "uz": "Kolleksiyani keyingi nashrga kiritish",
    "ru": "Включить коллекцию в следующую публикацию"
  },
  "Image kind": {
    "uz": "Rasm turi",
    "ru": "Вид изображения"
  },
  "unknown": {
    "uz": "noma’lum",
    "ru": "неизвестно"
  },
  "photo": {
    "uz": "fotosurat",
    "ru": "фотография"
  },
  "visualization": {
    "uz": "vizualizatsiya",
    "ru": "визуализация"
  },
  "archival": {
    "uz": "arxiv",
    "ru": "архивное"
  },
  "Credit": {
    "uz": "Muallif",
    "ru": "Автор"
  },
  "Rights / permission": {
    "uz": "Huquqlar / ruxsat",
    "ru": "Права / разрешение"
  },
  "Source URL": {
    "uz": "Manba havolasi",
    "ru": "Ссылка на источник"
  },
  "Approved for publication": {
    "uz": "Nashr uchun tasdiqlangan",
    "ru": "Одобрено для публикации"
  },
  "Attach images from Media to a product or logo slot.": {
    "uz": "Media bo‘limidagi rasmlarni mahsulot yoki logotip joyiga biriktiring.",
    "ru": "Прикрепите изображения из медиатеки к товару или месту логотипа."
  },
  "Publish a reviewed version": {
    "uz": "Tekshirilgan versiyani nashr qilish",
    "ru": "Опубликовать проверенную версию"
  },
  "Only active products with approved language copy and resolved questions appear. Draft prices, source notes, exact stock and unapproved images remain private.": {
    "uz": "Faqat matni tasdiqlangan va savollari hal qilingan faol mahsulotlar ko‘rsatiladi. Qoralama narxlar, manba izohlari, aniq zaxira va tasdiqlanmagan rasmlar yopiq qoladi.",
    "ru": "Показываются только активные товары с одобренным текстом и решёнными вопросами. Черновые цены, заметки, точные остатки и неодобренные изображения остаются закрытыми."
  },
  "Save your changes before publication.": {
    "uz": "Nashrdan oldin o‘zgarishlarni saqlang.",
    "ru": "Сохраните изменения перед публикацией."
  },
  "Release or withdrawal note": {
    "uz": "Nashr yoki bekor qilish izohi",
    "ru": "Комментарий к публикации или отзыву"
  },
  "Publish catalogue": {
    "uz": "Katalogni nashr qilish",
    "ru": "Опубликовать каталог"
  },
  "Release": {
    "uz": "Nashr",
    "ru": "Публикация"
  },
  "· Draft": {
    "uz": "· Qoralama",
    "ru": "· Черновик"
  },
  "View published catalogue data": {
    "uz": "Nashr qilingan katalog ma’lumotlarini ko‘rish",
    "ru": "Открыть данные опубликованного каталога"
  },
  "Withdraw with note": {
    "uz": "Izoh bilan bekor qilish",
    "ru": "Отозвать с комментарием"
  },
  "All drafts": {
    "uz": "Barcha qoralamalar",
    "ru": "Все черновики"
  },
  "For example uz, uz-Latn, ru, or en.": {
    "uz": "Masalan, uz, uz-Latn, ru yoki en.",
    "ru": "Например, uz, uz-Latn, ru или en."
  },
  "Working title": {
    "uz": "Ishchi sarlavha",
    "ru": "Рабочее название"
  },
  "Caption": {
    "uz": "Izoh matni",
    "ru": "Подпись"
  },
  "Intended destination": {
    "uz": "Mo‘ljallangan kanal",
    "ru": "Канал назначения"
  },
  "For example Instagram feed or Telegram channel.": {
    "uz": "Masalan, Instagram tasmasi yoki Telegram kanali.",
    "ru": "Например, лента Instagram или канал Telegram."
  },
  "Source notes": {
    "uz": "Manba izohlari",
    "ru": "Заметки об источниках"
  },
  "Record the facts and source records behind this copy. A render is not evidence of a finished product.": {
    "uz": "Matn uchun faktlar va manbalarni qayd eting. Render tayyor mahsulotning dalili emas.",
    "ru": "Укажите факты и источники текста. Рендер не подтверждает наличие готового товара."
  },
  "Images for this revision": {
    "uz": "Ushbu versiya rasmlari",
    "ru": "Изображения этой версии"
  },
  "Selection order becomes package order. Up to 10 images and 25 MiB combined.": {
    "uz": "Tanlash tartibi paket tartibi bo‘ladi. Ko‘pi bilan 10 ta rasm, jami 25 MiB.",
    "ru": "Порядок выбора станет порядком в пакете. До 10 изображений общим объёмом 25 МиБ."
  },
  "Order": {
    "uz": "Tartib",
    "ru": "Порядок"
  },
  "No images available. You can save a text-only draft, then add originals through Media.": {
    "uz": "Rasmlar yo‘q. Matnli qoralamani saqlab, keyin asl rasmlarni Media orqali qo‘shing.",
    "ru": "Изображений нет. Сохраните текстовый черновик, затем добавьте оригиналы через медиатеку."
  },
  "selected ·": {
    "uz": "tanlangan ·",
    "ru": "выбрано ·"
  },
  "Save revision": {
    "uz": "Versiyani saqlash",
    "ru": "Сохранить версию"
  },
  "Reload latest version": {
    "uz": "Oxirgi versiyani yuklash",
    "ru": "Загрузить последнюю версию"
  },
  "Saving creates a new immutable revision and clears its current review and approval. Unsaved changes are never included in review or export.": {
    "uz": "Saqlash yangi o‘zgarmas versiya yaratadi va joriy ko‘rib chiqish hamda tasdiqni bekor qiladi. Saqlanmagan o‘zgarishlar tekshiruv yoki eksportga kiritilmaydi.",
    "ru": "Сохранение создаёт неизменяемую версию и сбрасывает её текущую проверку и одобрение. Несохранённые изменения не попадают в проверку и экспорт."
  },
  "Review and manual export": {
    "uz": "Ko‘rib chiqish va qo‘lda eksport",
    "ru": "Проверка и ручной экспорт"
  },
  "Review the saved revision": {
    "uz": "Saqlangan versiyani tekshirish",
    "ru": "Проверить сохранённую версию"
  },
  "Save this draft before requesting review.": {
    "uz": "Tekshiruv so‘rashdan oldin qoralamani saqlang.",
    "ru": "Сохраните черновик перед запросом проверки."
  },
  "Actions apply to revision": {
    "uz": "Amallar qo‘llanadigan versiya",
    "ru": "Действия применяются к версии"
  },
  ": its exact caption, language, destination, source notes, and selected image bytes.": {
    "uz": ": aynan shu matn, til, kanal, manba izohlari va tanlangan rasmlarga.",
    "ru": ": к её точной подписи, языку, назначению, заметкам и выбранным файлам изображений."
  },
  "Save your changes before continuing with review or export.": {
    "uz": "Tekshiruv yoki eksportni davom ettirishdan oldin o‘zgarishlarni saqlang.",
    "ru": "Сохраните изменения перед продолжением проверки или экспорта."
  },
  "Review requested for revision": {
    "uz": "Tekshiruv so‘ralgan versiya",
    "ru": "Запрошена проверка версии"
  },
  "Revision": {
    "uz": "Versiya",
    "ru": "Версия"
  },
  "approved.": {
    "uz": "tasdiqlangan.",
    "ru": "одобрена."
  },
  "Approved by": {
    "uz": "Tasdiqlagan",
    "ru": "Одобрил"
  },
  "Audit reference": {
    "uz": "Audit havolasi",
    "ru": "Ссылка аудита"
  },
  "Operator": {
    "uz": "Operator",
    "ru": "Оператор"
  },
  "Request review": {
    "uz": "Tekshiruv so‘rash",
    "ru": "Запросить проверку"
  },
  "Owners may approve their own work in this initial workflow. Your operator identity is recorded.": {
    "uz": "Bu dastlabki jarayonda egalar o‘z ishlarini tasdiqlashi mumkin. Operator shaxsi qayd etiladi.",
    "ru": "На этом этапе владельцы могут одобрять собственную работу. Данные оператора сохраняются."
  },
  "Approve revision": {
    "uz": "Versiyani tasdiqlash",
    "ru": "Одобрить версию"
  },
  "An organization owner can approve this revision.": {
    "uz": "Tashkilot egasi ushbu versiyani tasdiqlashi mumkin.",
    "ru": "Владелец организации может одобрить эту версию."
  },
  "Revoke approval": {
    "uz": "Tasdiqni bekor qilish",
    "ru": "Отозвать одобрение"
  },
  "Reason for revocation": {
    "uz": "Bekor qilish sababi",
    "ru": "Причина отзыва"
  },
  "Confirm revocation": {
    "uz": "Bekor qilishni tasdiqlash",
    "ru": "Подтвердить отзыв"
  },
  "Manual handoff": {
    "uz": "Qo‘lda topshirish",
    "ru": "Ручная передача"
  },
  "Prepare an approved package": {
    "uz": "Tasdiqlangan paketni tayyorlash",
    "ru": "Подготовить одобренный пакет"
  },
  "The ZIP contains caption.txt, a source manifest, and the selected original media. Preparation does not confirm download or publication.": {
    "uz": "ZIP tarkibida caption.txt, manba manifesti va tanlangan asl media bor. Tayyorlash yuklash yoki nashrni tasdiqlamaydi.",
    "ru": "ZIP содержит caption.txt, манифест источников и выбранные оригинальные файлы. Подготовка не подтверждает скачивание или публикацию."
  },
  "Prepare ZIP": {
    "uz": "ZIP tayyorlash",
    "ru": "Подготовить ZIP"
  },
  "Package prepared from revision": {
    "uz": "Paket tayyorlangan versiya",
    "ru": "Пакет подготовлен из версии"
  },
  "Download approved ZIP": {
    "uz": "Tasdiqlangan ZIPni yuklab olish",
    "ru": "Скачать одобренный ZIP"
  },
  "The service returned an unexpected download location.": {
    "uz": "Xizmat kutilmagan yuklash manzilini qaytardi.",
    "ru": "Служба вернула неожиданный адрес скачивания."
  },
  "Publication remains unverified. Prepared copies cannot be recalled.": {
    "uz": "Nashr hali tasdiqlanmagan. Tayyorlangan nusxalarni qaytarib bo‘lmaydi.",
    "ru": "Публикация не подтверждена. Подготовленные копии нельзя отозвать."
  },
  "Content & preparation": {
    "uz": "Kontent va tayyorlash",
    "ru": "Контент и подготовка"
  },
  "A clear path to ready.": {
    "uz": "Tayyor natijaga aniq yo‘l.",
    "ru": "Понятный путь к готовому результату."
  },
  "Draft, review, and prepare an approved package for manual sharing.": {
    "uz": "Qoralama yarating, tekshiring va qo‘lda ulashish uchun tasdiqlangan paket tayyorlang.",
    "ru": "Создайте черновик, проверьте и подготовьте одобренный пакет для ручной отправки."
  },
  "New draft": {
    "uz": "Yangi qoralama",
    "ru": "Новый черновик"
  },
  "Start with a brand": {
    "uz": "Brenddan boshlang",
    "ru": "Начните с бренда"
  },
  "A draft belongs to a brand. An organization owner can create the first one.": {
    "uz": "Qoralama brendga tegishli. Tashkilot egasi birinchi brendni yaratishi mumkin.",
    "ru": "Черновик принадлежит бренду. Владелец организации может создать первый бренд."
  },
  "Open brands": {
    "uz": "Brendlarni ochish",
    "ru": "Открыть бренды"
  },
  "Saved drafts": {
    "uz": "Saqlangan qoralamalar",
    "ru": "Сохранённые черновики"
  },
  "· Revision": {
    "uz": "· Versiya",
    "ru": "· Версия"
  },
  "No drafts yet. Start with one real piece of content.": {
    "uz": "Hali qoralamalar yo‘q. Birinchi kontentdan boshlang.",
    "ru": "Черновиков пока нет. Начните с первого материала."
  },
  "Your creative workspace": {
    "uz": "Ijodiy ish maydoningiz",
    "ru": "Ваше творческое пространство"
  },
  "Open an app with your current organization. Access lasts five minutes; reconnect here when it expires.": {
    "uz": "Ilovani joriy tashkilot bilan oching. Kirish besh daqiqa amal qiladi; muddati tugaganda shu yerdan qayta ulaning.",
    "ru": "Откройте приложение с текущей организацией. Доступ действует пять минут; по истечении подключитесь здесь повторно."
  },
  "Connected apps are available from the configured local Ocharo workspace.": {
    "uz": "Ulangan ilovalar sozlangan mahalliy Ocharo ish maydonidan ochiladi.",
    "ru": "Подключённые приложения доступны из настроенного локального пространства Ocharo."
  },
  "Creative imports": {
    "uz": "Ijodiy importlar",
    "ru": "Импорт творческих материалов"
  },
  "Studio renders arrive as unreviewed images. Review their source and rights before adding them to your catalogue.": {
    "uz": "Studio renderlari tekshirilmagan rasmlar sifatida keladi. Katalogga qo‘shishdan oldin manba va huquqlarini tekshiring.",
    "ru": "Рендеры Studio поступают как непроверенные изображения. Проверьте источники и права перед добавлением в каталог."
  },
  "Studio manifest": {
    "uz": "Studio manifesti",
    "ru": "Манифест Studio"
  },
  "Exported image": {
    "uz": "Eksport qilingan rasm",
    "ru": "Экспортированное изображение"
  },
  "Product (optional)": {
    "uz": "Mahsulot (ixtiyoriy)",
    "ru": "Товар (необязательно)"
  },
  "Assign during review": {
    "uz": "Tekshiruv vaqtida belgilash",
    "ru": "Назначить при проверке"
  },
  "Choose both files downloaded by Studio. The image fingerprint must match. Studio's author submission does not approve catalogue publication.": {
    "uz": "Studiodan yuklangan ikkala faylni tanlang. Rasm xeshi mos kelishi kerak. Studio muallifi yuborishi katalog nashrini tasdiqlamaydi.",
    "ru": "Выберите оба файла из Studio. Хеш изображения должен совпасть. Отправка автором Studio не одобряет публикацию каталога."
  },
  "Loading creative imports…": {
    "uz": "Ijodiy importlar yuklanmoqda…",
    "ru": "Загрузка творческих материалов…"
  },
  "No Studio renders have been imported for this brand.": {
    "uz": "Bu brend uchun Studio renderlari import qilinmagan.",
    "ru": "Для этого бренда ещё не импортированы рендеры Studio."
  },
  "Look": {
    "uz": "Obraz",
    "ru": "Образ"
  },
  "asset releases ·": {
    "uz": "resurs nashrlari ·",
    "ru": "публикаций ресурсов ·"
  },
  "materials recorded": {
    "uz": "material qayd etilgan",
    "ru": "материалов записано"
  },
  "Review in Products": {
    "uz": "Mahsulotlarda tekshirish",
    "ru": "Проверить в разделе товаров"
  },
  "Your collection.": {
    "uz": "Sizning kolleksiyangiz.",
    "ru": "Ваша коллекция."
  },
  "Your next chapter.": {
    "uz": "Sizning yangi bosqichingiz.",
    "ru": "Ваша новая глава."
  },
  "Design your catalogue, refine your business card, and bring your brand’s work together.": {
    "uz": "Katalogingizni yarating, tashrifnomangizni takomillashtiring va brend ishlarini bir joyda jamlang.",
    "ru": "Создайте каталог, доработайте визитку и объедините работу бренда."
  },
  "Prepare and export manually. Social publishing is not connected.": {
    "uz": "Qo‘lda tayyorlang va eksport qiling. Ijtimoiy tarmoqlarda nashr qilish ulanmagan.",
    "ru": "Подготавливайте и экспортируйте вручную. Публикация в соцсетях не подключена."
  },
  "Operator sign-in": {
    "uz": "Operator kirishi",
    "ru": "Вход оператора"
  },
  "An administrator needs to provision the first operator before sign-in is available.": {
    "uz": "Kirishdan oldin administrator birinchi operatorni yaratishi kerak.",
    "ru": "Для входа администратор должен создать первого оператора."
  },
  "This workspace does not offer public registration.": {
    "uz": "Bu ish maydonida ochiq ro‘yxatdan o‘tish yo‘q.",
    "ru": "В этом пространстве нет открытой регистрации."
  },
  "Email": {
    "uz": "Elektron pochta",
    "ru": "Электронная почта"
  },
  "Password": {
    "uz": "Parol",
    "ru": "Пароль"
  },
  "Sign-in was not completed. Check your credentials and retry; repeated failures may temporarily lock the account.": {
    "uz": "Kirish amalga oshmadi. Ma’lumotlarni tekshirib qayta urinib ko‘ring; takroriy xatolar hisobni vaqtincha bloklashi mumkin.",
    "ru": "Вход не выполнен. Проверьте данные и повторите; повторные ошибки могут временно заблокировать учётную запись."
  },
  "Use the account provisioned by your administrator.": {
    "uz": "Administrator yaratgan hisobdan foydalaning.",
    "ru": "Используйте учётную запись, созданную администратором."
  },
  "Originals & provenance": {
    "uz": "Asl nusxalar va manbalar",
    "ru": "Оригиналы и происхождение"
  },
  "Media library": {
    "uz": "Media kutubxonasi",
    "ru": "Медиатека"
  },
  "Keep your original images together with their source details.": {
    "uz": "Asl rasmlarni manba ma’lumotlari bilan birga saqlang.",
    "ru": "Храните оригиналы изображений вместе с данными об источниках."
  },
  "Add an image": {
    "uz": "Rasm qo‘shish",
    "ru": "Добавить изображение"
  },
  "Choose an original image": {
    "uz": "Asl rasmni tanlang",
    "ru": "Выберите оригинал изображения"
  },
  "Selected:": {
    "uz": "Tanlangan:",
    "ru": "Выбрано:"
  },
  "Source and provenance": {
    "uz": "Manba va kelib chiqishi",
    "ru": "Источник и происхождение"
  },
  "Record the creator and source, and whether this is a photograph or a visualization.": {
    "uz": "Muallif, manba va bu fotosurat yoki vizualizatsiya ekanini qayd eting.",
    "ru": "Укажите автора, источник и тип: фотография или визуализация."
  },
  "Image added.": {
    "uz": "Rasm qo‘shildi.",
    "ru": "Изображение добавлено."
  },
  "Upload image": {
    "uz": "Rasm yuklash",
    "ru": "Загрузить изображение"
  },
  "Uploaded media": {
    "uz": "Yuklangan media",
    "ru": "Загруженные файлы"
  },
  "MiB ·": {
    "uz": "MiB ·",
    "ru": "МиБ ·"
  },
  "Integrity fingerprint": {
    "uz": "Yaxlitlik xeshi",
    "ru": "Хеш целостности"
  },
  "No media has been uploaded to this organization.": {
    "uz": "Bu tashkilotga hali media yuklanmagan.",
    "ru": "В эту организацию ещё не загружены файлы."
  },
  "Your memberships": {
    "uz": "A’zoliklaringiz",
    "ru": "Ваши организации"
  },
  "You do not belong to an organization yet. Create one to begin.": {
    "uz": "Hali tashkilot a’zosi emassiz. Boshlash uchun tashkilot yarating.",
    "ru": "Вы ещё не состоите в организации. Создайте её для начала."
  },
  "Create an organization": {
    "uz": "Tashkilot yaratish",
    "ru": "Создать организацию"
  },
  "You will become its owner. Nothing is prefilled or created for you.": {
    "uz": "Siz uning egasi bo‘lasiz. Hech narsa avtomatik to‘ldirilmaydi yoki yaratilmaydi.",
    "ru": "Вы станете владельцем. Ничего не заполняется и не создаётся автоматически."
  },
  "Organization name": {
    "uz": "Tashkilot nomi",
    "ru": "Название организации"
  },
  "Short name": {
    "uz": "Qisqa nom",
    "ru": "Короткое имя"
  },
  "Lowercase letters, numbers, and hyphens.": {
    "uz": "Kichik harflar, raqamlar va chiziqchalar.",
    "ru": "Строчные буквы, цифры и дефисы."
  },
  "Create organization": {
    "uz": "Tashkilot yaratish",
    "ru": "Создать организацию"
  },
  "Your business, in view": {
    "uz": "Biznesingiz bir qarashda",
    "ru": "Ваш бизнес на виду"
  },
  "The work,": {
    "uz": "Ishlar,",
    "ru": "Всё дело,"
  },
  "connected.": {
    "uz": "birga.",
    "ru": "вместе."
  },
  "A place for": {
    "uz": "Joy",
    "ru": "Место для"
  },
  "to take shape.": {
    "uz": "shakllanishi uchun.",
    "ru": "и его развития."
  },
  "From the collection to the next conversation.": {
    "uz": "Kolleksiyadan keyingi suhbatgacha.",
    "ru": "От коллекции до следующего разговора."
  },
  "Make the space yours": {
    "uz": "Maydonni o‘zingizga moslang",
    "ru": "Настройте пространство под себя"
  },
  "Overview view": {
    "uz": "Umumiy ko‘rinish",
    "ru": "Вид обзора"
  },
  "Saved design totals are unavailable. Content and media still show their own records.": {
    "uz": "Saqlangan dizaynlar soni mavjud emas. Kontent va media o‘z yozuvlarini ko‘rsatmoqda.",
    "ru": "Число сохранённых дизайнов недоступно. Контент и медиа показывают свои записи."
  },
  "Retry totals": {
    "uz": "Sonlarni qayta yuklash",
    "ru": "Повторить подсчёт"
  },
  "01 / THE WORK MAP": {
    "uz": "01 / ISHLAR XARITASI",
    "ru": "01 / КАРТА РАБОТЫ"
  },
  "Every piece has a place.": {
    "uz": "Har bir ishning o‘z joyi bor.",
    "ru": "У каждого дела своё место."
  },
  "Your connected workflow": {
    "uz": "Bog‘langan ish jarayoningiz",
    "ru": "Ваш связанный рабочий процесс"
  },
  "Your collection": {
    "uz": "Kolleksiyangiz",
    "ru": "Ваша коллекция"
  },
  "saved designs": {
    "uz": "saqlangan dizaynlar",
    "ru": "сохранённых дизайнов"
  },
  "The starting point for what comes next": {
    "uz": "Keyingi ishlar uchun boshlanish nuqtasi",
    "ru": "Отправная точка для следующего шага"
  },
  "The map shows how your work fits together. Moving content between apps is intentional.": {
    "uz": "Xarita ishlaringiz qanday bog‘lanishini ko‘rsatadi. Kontent ilovalar orasida sizning tanlovingiz bilan ko‘chiriladi.",
    "ru": "Карта показывает связи между задачами. Вы сами решаете, когда переносить контент между приложениями."
  },
  "A LITTLE ATTENTION": {
    "uz": "BIR OZ E’TIBOR",
    "ru": "НЕМНОГО ВНИМАНИЯ"
  },
  "THE CREATIVE COMPANION": {
    "uz": "IJODIY HAMROH",
    "ru": "ТВОРЧЕСКИЙ ПОМОЩНИК"
  },
  "Studio": {
    "uz": "Studiya",
    "ru": "Студия"
  },
  "Explore garments, textiles and colour. Bring a chosen design into the business as a deliberate export.": {
    "uz": "Kiyim, mato va ranglarni o‘rganing. Tanlangan dizaynni eksport orqali biznesga olib kiring.",
    "ru": "Изучайте одежду, ткани и цвет. Перенесите выбранный дизайн в бизнес через экспорт."
  },
  "Separate app · not connected here": {
    "uz": "Alohida ilova · bu yerga ulanmagan",
    "ru": "Отдельное приложение · здесь не подключено"
  },
  "Content preparation status": {
    "uz": "Kontent tayyorlash holati",
    "ru": "Состояние подготовки контента"
  },
  "YOUR CONTENT, RIGHT NOW": {
    "uz": "KONTENTINGIZ HOZIR",
    "ru": "ВАШ КОНТЕНТ СЕЙЧАС"
  },
  "A clearer place": {
    "uz": "Qulayroq joy",
    "ru": "Удобное место,"
  },
  "to pick up the thread.": {
    "uz": "ishni davom ettirish uchun.",
    "ru": "чтобы продолжить работу."
  },
  "In the making": {
    "uz": "Tayyorlanmoqda",
    "ru": "В работе"
  },
  "In review": {
    "uz": "Tekshiruvda",
    "ru": "На проверке"
  },
  "Approved": {
    "uz": "Tasdiqlangan",
    "ru": "Одобрено"
  },
  "Things to pick up": {
    "uz": "Davom ettiriladigan ishlar",
    "ru": "Что продолжить"
  },
  "A QUIETER MOMENT": {
    "uz": "OSOYISHTA LAHZA",
    "ru": "СПОКОЙНЫЙ МОМЕНТ"
  },
  "Nothing waiting for review.": {
    "uz": "Tekshiruvni kutayotgan ishlar yo‘q.",
    "ru": "Ничего не ожидает проверки."
  },
  "Your saved work is here whenever you want to return to it.": {
    "uz": "Saqlangan ishlaringiz qaytishingizni kutmoqda.",
    "ru": "Сохранённая работа доступна, когда захотите вернуться."
  },
  "Open catalogue": {
    "uz": "Katalogni ochish",
    "ru": "Открыть каталог"
  },
  "Everything has its place": {
    "uz": "Har narsaning o‘z joyi bor",
    "ru": "У всего есть своё место"
  },
  "THE ESSENTIALS": {
    "uz": "ASOSIY BO‘LIMLAR",
    "ru": "ОСНОВНОЕ"
  },
  "Your working collection.": {
    "uz": "Ishchi kolleksiyangiz.",
    "ru": "Ваша рабочая коллекция."
  },
  "PICK UP THE THREAD": {
    "uz": "ISHNI DAVOM ETTIRING",
    "ru": "ПРОДОЛЖИТЬ РАБОТУ"
  },
  "Ready for review": {
    "uz": "Tekshiruvga tayyor",
    "ru": "Готово к проверке"
  },
  "Approved content": {
    "uz": "Tasdiqlangan kontent",
    "ru": "Одобренный контент"
  },
  "ON YOUR DESK": {
    "uz": "ISH STOLINGIZDA",
    "ru": "НА ВАШЕМ СТОЛЕ"
  },
  "Recent content": {
    "uz": "Oxirgi kontent",
    "ru": "Последние материалы"
  },
  "All content": {
    "uz": "Barcha kontent",
    "ru": "Все материалы"
  },
  "Future connections": {
    "uz": "Kelajakdagi ulanishlar",
    "ru": "Будущие подключения"
  },
  "BEYOND THE WORKSPACE": {
    "uz": "ISH MAYDONIDAN TASHQARIDA",
    "ru": "ЗА ПРЕДЕЛАМИ ПРОСТРАНСТВА"
  },
  "More of the business, when it is ready.": {
    "uz": "Biznesning keyingi qismlari tayyor bo‘lganda.",
    "ru": "Другие части бизнеса — когда они будут готовы."
  },
  "Marketing": {
    "uz": "Marketing",
    "ru": "Маркетинг"
  },
  "Planned": {
    "uz": "Rejalashtirilgan",
    "ru": "Запланировано"
  },
  "Journal": {
    "uz": "Blog",
    "ru": "Блог"
  },
  "Not connected": {
    "uz": "Ulanmagan",
    "ru": "Не подключено"
  },
  "Scheduling": {
    "uz": "Rejalashtirish",
    "ru": "Планирование"
  },
  "Separate app": {
    "uz": "Alohida ilova",
    "ru": "Отдельное приложение"
  },
  "Loading saved design totals…": {
    "uz": "Saqlangan dizaynlar soni yuklanmoqda…",
    "ru": "Загрузка числа сохранённых дизайнов…"
  },
  "Product content language": {
    "uz": "Mahsulot matni tili",
    "ru": "Язык текста товара"
  },
  "Checking your session…": {
    "uz": "Sessiyangiz tekshirilmoqda…",
    "ru": "Проверка сеанса…"
  },
  "Session unavailable": {
    "uz": "Sessiya mavjud emas",
    "ru": "Сеанс недоступен"
  },
  "Retry session": {
    "uz": "Sessiyani qayta tekshirish",
    "ru": "Повторить проверку сеанса"
  },
  "Rechecking your session…": {
    "uz": "Sessiyangiz qayta tekshirilmoqda…",
    "ru": "Повторная проверка сеанса…"
  },
  "Verify your session to continue": {
    "uz": "Davom etish uchun sessiyani tasdiqlang",
    "ru": "Подтвердите сеанс для продолжения"
  },
  "This page keeps your unsaved edits while the session is checked.": {
    "uz": "Sessiya tekshirilayotganda sahifa saqlanmagan o‘zgarishlarni saqlab turadi.",
    "ru": "При проверке сеанса страница сохраняет несохранённые изменения."
  },
  "Some workspace information could not refresh. Your design remains open.": {
    "uz": "Ayrim ma’lumotlar yangilanmadi. Dizayningiz ochiq qoladi.",
    "ru": "Часть данных не удалось обновить. Дизайн остаётся открытым."
  },
  "Ocharo · A place for your collection, identity, and stories. Social publishing is not connected.": {
    "uz": "Ocharo · Kolleksiyangiz, brendingiz va hikoyalaringiz uchun joy. Ijtimoiy tarmoqlarda nashr qilish ulanmagan.",
    "ru": "Ocharo · Пространство коллекции, бренда и историй. Публикация в соцсетях не подключена."
  },
  "Active": {
    "uz": "Faol",
    "ru": "Активный"
  },
  "Add image group": {
    "uz": "Rasmlar guruhi qo‘shish",
    "ru": "Добавить группу изображений"
  },
  "Add price": {
    "uz": "Narx qo‘shish",
    "ru": "Добавить цену"
  },
  "Add quantity price": {
    "uz": "Miqdor narxi qo‘shish",
    "ru": "Добавить цену для количества"
  },
  "Approve this language for publication": {
    "uz": "Ushbu tilni nashr uchun tasdiqlash",
    "ru": "Одобрить этот язык для публикации"
  },
  "Archived": {
    "uz": "Arxivlangan",
    "ru": "Архивный"
  },
  "Category": {
    "uz": "Turkum",
    "ru": "Категория"
  },
  "Confirm prices for publication": {
    "uz": "Narxlarni nashr uchun tasdiqlash",
    "ru": "Подтвердить цены для публикации"
  },
  "Content language": {
    "uz": "Kontent tili",
    "ru": "Язык контента"
  },
  "Currency": {
    "uz": "Valyuta",
    "ru": "Валюта"
  },
  "Decoration": {
    "uz": "Bezak",
    "ru": "Отделка"
  },
  "Detach": {
    "uz": "Ajratish",
    "ru": "Открепить"
  },
  "Details": {
    "uz": "Tafsilotlar",
    "ru": "Детали"
  },
  "Draft": {
    "uz": "Qoralama",
    "ru": "Черновик"
  },
  "Edit amounts here. The catalogue controls which price rows appear.": {
    "uz": "Summalarni shu yerda tahrirlang. Katalog qaysi narx qatorlari ko‘rinishini belgilaydi.",
    "ru": "Изменяйте суммы здесь. Каталог определяет, какие строки цен показывать."
  },
  "Fabric photos": {
    "uz": "Mato rasmlari",
    "ru": "Фотографии ткани"
  },
  "Group model photos or product views to switch the whole catalogue together.": {
    "uz": "Butun katalogda birgalikda almashtirish uchun model yoki mahsulot rasmlarini guruhlang.",
    "ru": "Сгруппируйте фотографии моделей или товаров для переключения во всём каталоге."
  },
  "Group name": {
    "uz": "Guruh nomi",
    "ru": "Название группы"
  },
  "Image category": {
    "uz": "Rasm turkumi",
    "ru": "Категория изображения"
  },
  "Image description": {
    "uz": "Rasm tavsifi",
    "ru": "Описание изображения"
  },
  "Image groups": {
    "uz": "Rasmlar guruhlari",
    "ru": "Группы изображений"
  },
  "Image role": {
    "uz": "Rasm vazifasi",
    "ru": "Роль изображения"
  },
  "Images currently use their roles. Add groups for explicit photo selections.": {
    "uz": "Rasmlar hozir o‘z vazifalari bo‘yicha tanlanadi. Aniq tanlovlar uchun guruh qo‘shing.",
    "ru": "Сейчас изображения выбираются по ролям. Добавьте группы для явного выбора фотографий."
  },
  "Made to order": {
    "uz": "Buyurtma asosida",
    "ru": "На заказ"
  },
  "Material / fabric name": {
    "uz": "Material / mato nomi",
    "ru": "Материал / название ткани"
  },
  "Material codes, separated by commas": {
    "uz": "Material kodlari, vergul bilan",
    "ru": "Коды материалов через запятую"
  },
  "Maximum quantity": {
    "uz": "Eng ko‘p miqdor",
    "ru": "Максимальное количество"
  },
  "Minimum quantity": {
    "uz": "Eng kam miqdor",
    "ru": "Минимальное количество"
  },
  "Model photos": {
    "uz": "Modeldagi rasmlar",
    "ru": "Фотографии на модели"
  },
  "New image group": {
    "uz": "Yangi rasmlar guruhi",
    "ru": "Новая группа изображений"
  },
  "No maximum": {
    "uz": "Cheklanmagan",
    "ru": "Без ограничения"
  },
  "Photo": {
    "uz": "Fotosurat",
    "ru": "Фотография"
  },
  "Price per piece": {
    "uz": "Dona narxi",
    "ru": "Цена за штуку"
  },
  "Private reference": {
    "uz": "Ichki namuna",
    "ru": "Внутренний референс"
  },
  "Product images": {
    "uz": "Mahsulot rasmlari",
    "ru": "Изображения товара"
  },
  "Product name": {
    "uz": "Mahsulot nomi",
    "ru": "Название товара"
  },
  "Product prices": {
    "uz": "Mahsulot narxlari",
    "ru": "Цены товара"
  },
  "Product translations": {
    "uz": "Mahsulot tarjimalari",
    "ru": "Переводы товара"
  },
  "Product views": {
    "uz": "Mahsulot ko‘rinishlari",
    "ru": "Виды товара"
  },
  "Product wording appears in every catalogue using this product.": {
    "uz": "Mahsulot matni undan foydalanuvchi har bir katalogda ko‘rsatiladi.",
    "ru": "Текст товара появляется во всех каталогах, использующих этот товар."
  },
  "Remove group": {
    "uz": "Guruhni olib tashlash",
    "ru": "Удалить группу"
  },
  "Sizes, separated by commas": {
    "uz": "O‘lchamlar, vergul bilan",
    "ru": "Размеры через запятую"
  },
  "Status": {
    "uz": "Holat",
    "ru": "Статус"
  },
  "This translation is empty. Add wording before approving it.": {
    "uz": "Bu tarjima bo‘sh. Tasdiqlashdan oldin matn qo‘shing.",
    "ru": "Этот перевод пуст. Добавьте текст перед одобрением."
  },
  "Variant code": {
    "uz": "Variant kodi",
    "ru": "Код варианта"
  },
  "Brand identity": {
    "uz": "Brend qiyofasi",
    "ru": "Оформление бренда"
  },
  "Collections": {
    "uz": "Kolleksiyalar",
    "ru": "Коллекции"
  },
  "Image review": {
    "uz": "Rasmlarni tekshirish",
    "ru": "Проверка изображений"
  },
  "Publish & history": {
    "uz": "Nashr va tarix",
    "ru": "Публикация и история"
  },
  "Withdrawn": {
    "uz": "Bekor qilingan",
    "ru": "Отозвано"
  },
  "Published": {
    "uz": "Nashr qilingan",
    "ru": "Опубликовано"
  },
  "owner": {
    "uz": "Egasi",
    "ru": "Владелец"
  },
  "editor": {
    "uz": "Muharrir",
    "ru": "Редактор"
  },
  "viewer": {
    "uz": "Ko‘ruvchi",
    "ru": "Читатель"
  },
  "Choose your catalogue workspace": {
    "uz": "Katalog ish maydonini tanlang",
    "ru": "Выберите пространство каталога"
  },
  "Choose a workspace": {
    "uz": "Ish maydonini tanlang",
    "ru": "Выберите пространство"
  },
  "Open the catalogue designer for one of your organizations.": {
    "uz": "Tashkilotlaringizdan biri uchun katalog muharririni oching.",
    "ru": "Откройте редактор каталога для одной из ваших организаций."
  },
  "Each organization keeps its own brands, media, and content.": {
    "uz": "Har bir tashkilot o‘z brendlari, media va kontentini saqlaydi.",
    "ru": "Каждая организация хранит собственные бренды, медиа и контент."
  },
  "Connecting…": {
    "uz": "Ulanmoqda…",
    "ru": "Подключение…"
  },
  "Connect to {name}": {
    "uz": "{name} bilan ulanish",
    "ru": "Подключиться к {name}"
  },
  "Discard the selected import files and open this app?": {
    "uz": "Tanlangan import fayllaridan voz kechib, ilovani ochasizmi?",
    "ru": "Отменить выбранные файлы импорта и открыть приложение?"
  },
  "Discard unsaved changes and leave this page?": {
    "uz": "Saqlanmagan o‘zgarishlardan voz kechib, sahifadan chiqasizmi?",
    "ru": "Отменить несохранённые изменения и покинуть страницу?"
  },
  "Discard unsaved changes and sign out?": {
    "uz": "Saqlanmagan o‘zgarishlardan voz kechib, hisobdan chiqasizmi?",
    "ru": "Отменить несохранённые изменения и выйти?"
  },
  "Create garment looks and bring reviewed renders into your product library.": {
    "uz": "Kiyim obrazlarini yarating va tekshirilgan renderlarni mahsulot kutubxonasiga olib kiring.",
    "ru": "Создавайте образы и добавляйте проверенные рендеры в библиотеку товаров."
  },
  "Prepare campaigns using your organization and approved brand releases.": {
    "uz": "Tashkilotingiz va tasdiqlangan brend nashrlari asosida kampaniyalar tayyorlang.",
    "ru": "Готовьте кампании с данными организации и одобренными публикациями бренда."
  },
  "Write and review stories in the editorial workspace.": {
    "uz": "Tahririyat ish maydonida hikoyalar yozing va tekshiring.",
    "ru": "Пишите и проверяйте истории в редакционном пространстве."
  },
  "Sign-in requires HTTPS. Open the secure application URL, then retry.": {
    "uz": "Kirish uchun HTTPS kerak. Ilovaning xavfsiz manzilini ochib qayta urinib ko‘ring.",
    "ru": "Для входа нужен HTTPS. Откройте защищённый адрес приложения и повторите."
  },
  "This record changed. Your edits are still here. Reload the latest version before trying again.": {
    "uz": "Yozuv o‘zgardi. Tahrirlaringiz saqlanib turibdi. Qayta urinishdan oldin oxirgi versiyani yuklang.",
    "ru": "Запись изменилась. Ваши правки сохранены. Загрузите последнюю версию перед повтором."
  },
  "Check the entered values and try again.": {
    "uz": "Kiritilgan ma’lumotlarni tekshirib qayta urinib ko‘ring.",
    "ru": "Проверьте введённые значения и повторите."
  },
  "Your session is no longer valid. Sign in again.": {
    "uz": "Sessiya muddati tugagan. Qayta kiring.",
    "ru": "Сеанс недействителен. Войдите снова."
  },
  "This action is not permitted, or your session changed. Reload to check your access.": {
    "uz": "Amalga ruxsat yo‘q yoki sessiya o‘zgardi. Kirish huquqini tekshirish uchun qayta yuklang.",
    "ru": "Действие запрещено или сеанс изменился. Перезагрузите для проверки доступа."
  },
  "This record is unavailable in this organization.": {
    "uz": "Bu yozuv ushbu tashkilotda mavjud emas.",
    "ru": "Эта запись недоступна в данной организации."
  },
  "The service returned an unexpected response. Please retry.": {
    "uz": "Xizmat kutilmagan javob qaytardi. Qayta urinib ko‘ring.",
    "ru": "Служба вернула неожиданный ответ. Повторите попытку."
  },
  "The request was cancelled. Your edits have been retained.": {
    "uz": "So‘rov bekor qilindi. Tahrirlaringiz saqlanib turibdi.",
    "ru": "Запрос отменён. Ваши правки сохранены."
  },
  "The service could not complete this request. Your edits are still here; try again.": {
    "uz": "Xizmat so‘rovni bajara olmadi. Tahrirlaringiz saqlanib turibdi; qayta urinib ko‘ring.",
    "ru": "Служба не выполнила запрос. Ваши правки сохранены; повторите попытку."
  },
  "Session verification is unavailable. Retry before continuing.": {
    "uz": "Sessiyani tekshirish mavjud emas. Davom etishdan oldin qayta urinib ko‘ring.",
    "ru": "Проверка сеанса недоступна. Повторите перед продолжением."
  },
  "unsaved": {
    "uz": "saqlanmagan",
    "ru": "не сохранён"
  },
  "master": {
    "uz": "Asosiy",
    "ru": "Основной"
  },
  "on-light": {
    "uz": "Och fonda",
    "ru": "На светлом фоне"
  },
  "on-dark": {
    "uz": "To‘q fonda",
    "ru": "На тёмном фоне"
  },
  "avatar": {
    "uz": "Avatar",
    "ru": "Аватар"
  },
  "favicon": {
    "uz": "Brauzer belgisi",
    "ru": "Значок браузера"
  },
  "social": {
    "uz": "Ijtimoiy tarmoq",
    "ru": "Соцсети"
  },
  "Image awaiting provenance review": {
    "uz": "Manbasi tekshirilayotgan rasm",
    "ru": "Изображение ожидает проверки источника"
  },
  "Draft revision {revision} saved.": {
    "uz": "Qoralamaning {revision}-versiyasi saqlandi.",
    "ru": "Версия черновика {revision} сохранена."
  },
  "Release {revision} published. Your approved catalogue is available to connected public apps.": {
    "uz": "{revision}-nashr chop etildi. Tasdiqlangan katalog ulangan ommaviy ilovalarda mavjud.",
    "ru": "Публикация {revision} выпущена. Одобренный каталог доступен подключённым публичным приложениям."
  },
  "Preserved records copied into a private draft. Review image provenance before publication.": {
    "uz": "Saqlangan yozuvlar yopiq qoralamaga ko‘chirildi. Nashrdan oldin rasmlar manbasini tekshiring.",
    "ru": "Сохранённые записи скопированы в закрытый черновик. Проверьте происхождение изображений перед публикацией."
  },
  "Release withdrawn. Pinned public links now return unavailable.": {
    "uz": "Nashr bekor qilindi. Unga havolalar endi mavjud emas.",
    "ru": "Публикация отозвана. Закреплённые публичные ссылки теперь недоступны."
  },
  "Studio image imported for review. Attach it from Media to a product, then review its provenance before publication.": {
    "uz": "Studio rasmi tekshiruv uchun import qilindi. Mediadan mahsulotga biriktiring va nashrdan oldin manbasini tekshiring.",
    "ru": "Изображение Studio импортировано для проверки. Прикрепите его из медиатеки к товару и проверьте источник перед публикацией."
  },
  "The Studio import could not complete.": {
    "uz": "Studio importi yakunlanmadi.",
    "ru": "Импорт из Studio не завершён."
  },
  "Importing…": {
    "uz": "Import qilinmoqda…",
    "ru": "Импорт…"
  },
  "Import for review": {
    "uz": "Tekshiruv uchun import",
    "ru": "Импортировать для проверки"
  },
  "Work map": {
    "uz": "Ishlar xaritasi",
    "ru": "Карта работы"
  },
  "See how the pieces connect.": {
    "uz": "Ishlar qanday bog‘langanini ko‘ring.",
    "ru": "Посмотрите, как связаны части работы."
  },
  "Attention board": {
    "uz": "E’tibor paneli",
    "ru": "Доска внимания"
  },
  "Find what needs your attention.": {
    "uz": "E’tiboringiz talab qilinadigan ishlarni toping.",
    "ru": "Найдите то, что требует внимания."
  },
  "Compact dashboard": {
    "uz": "Ixcham panel",
    "ru": "Компактная панель"
  },
  "Everything in a quieter list.": {
    "uz": "Hammasi ixcham ro‘yxatda.",
    "ru": "Всё в спокойном списке."
  },
  "Your workspace": {
    "uz": "Ish maydoningiz",
    "ru": "Ваше пространство"
  },
  "The collection, presented": {
    "uz": "Kolleksiya taqdimoti",
    "ru": "Презентация коллекции"
  },
  "A personal introduction": {
    "uz": "Shaxsiy tanishtiruv",
    "ru": "Личное знакомство"
  },
  "Open business cards": {
    "uz": "Tashrifnomalarni ochish",
    "ru": "Открыть визитки"
  },
  "The original material": {
    "uz": "Asl materiallar",
    "ru": "Исходные материалы"
  },
  "uploaded assets": {
    "uz": "yuklangan fayllar",
    "ru": "загруженных файлов"
  },
  "Open media library": {
    "uz": "Media kutubxonasini ochish",
    "ru": "Открыть медиатеку"
  },
  "Content desk": {
    "uz": "Kontent ish stoli",
    "ru": "Стол контента"
  },
  "Words, images, review": {
    "uz": "Matnlar, rasmlar, tekshiruv",
    "ru": "Тексты, изображения, проверка"
  },
  "content drafts": {
    "uz": "kontent qoralamalari",
    "ru": "черновиков контента"
  },
  "Open content desk": {
    "uz": "Kontent bo‘limini ochish",
    "ru": "Открыть контент"
  },
  "What holds it together": {
    "uz": "Barchasini birlashtiradi",
    "ru": "То, что объединяет всё"
  },
  "brands": {
    "uz": "brendlar",
    "ru": "брендов"
  },
  "{count} drafts ready for review": {
    "uz": "{count} ta qoralama tekshiruvga tayyor",
    "ru": "Черновиков к проверке: {count}"
  },
  "Read the exact revision before it is approved.": {
    "uz": "Tasdiqlashdan oldin aynan shu versiyani o‘qing.",
    "ru": "Прочитайте именно эту версию перед одобрением."
  },
  "Review content": {
    "uz": "Kontentni tekshirish",
    "ru": "Проверить контент"
  },
  "Your first catalogue is waiting": {
    "uz": "Birinchi katalogingiz sizni kutmoqda",
    "ru": "Ваш первый каталог ждёт"
  },
  "Start with a layout, bring in your products, and save an edition.": {
    "uz": "Maketdan boshlang, mahsulotlarni qo‘shing va katalog versiyasini saqlang.",
    "ru": "Начните с макета, добавьте товары и сохраните выпуск."
  },
  "Explore catalogue": {
    "uz": "Katalogni ochish",
    "ru": "Открыть каталог"
  },
  "Make an introduction to keep": {
    "uz": "Esda qoladigan tanishtiruv yarating",
    "ru": "Создайте знакомство, которое сохранят"
  },
  "Choose a business-card direction and make it your own.": {
    "uz": "Tashrifnoma uslubini tanlang va o‘zingizga moslang.",
    "ru": "Выберите стиль визитки и адаптируйте его."
  },
  "Explore business cards": {
    "uz": "Tashrifnomalarni ko‘rish",
    "ru": "Посмотреть визитки"
  },
  "Give your work a brand home": {
    "uz": "Ishlaringiz uchun brend yarating",
    "ru": "Создайте бренд для своей работы"
  },
  "A brand keeps your content and its identity together.": {
    "uz": "Brend kontent va uning qiyofasini birlashtiradi.",
    "ru": "Бренд объединяет контент и его оформление."
  },
  "Bring in your original images": {
    "uz": "Asl rasmlaringizni qo‘shing",
    "ru": "Добавьте оригиналы изображений"
  },
  "Keep source photographs and their provenance together.": {
    "uz": "Asl fotosuratlarni manbalari bilan birga saqlang.",
    "ru": "Храните исходные фотографии вместе с данными об их происхождении."
  },
  "{count} drafts in the making": {
    "uz": "{count} ta qoralama tayyorlanmoqda",
    "ru": "Черновиков в работе: {count}"
  },
  "Pick up an unfinished piece when you are ready.": {
    "uz": "Tayyor bo‘lganingizda tugallanmagan ishni davom ettiring.",
    "ru": "Продолжите незавершённую работу, когда будете готовы."
  },
  "Continue content": {
    "uz": "Kontentni davom ettirish",
    "ru": "Продолжить контент"
  },
  "View changed for this visit. Your browser could not remember the preference.": {
    "uz": "Ko‘rinish joriy tashrif uchun o‘zgardi. Brauzer tanlovni eslab qola olmadi.",
    "ru": "Вид изменён для этого посещения. Браузер не смог запомнить выбор."
  },
  "A place for {name} to take shape.": {
    "uz": "{name} rivojlanishi uchun joy.",
    "ru": "Место для развития {name}."
  },
  "Room for your next idea.": {
    "uz": "Yangi g‘oyangiz uchun joy.",
    "ru": "Место для следующей идеи."
  },
  "No drafts are waiting for review. Open your collection whenever inspiration returns.": {
    "uz": "Tekshiruvni kutayotgan qoralamalar yo‘q. Ilhom kelganda kolleksiyangizni oching.",
    "ru": "Черновиков на проверке нет. Откройте коллекцию, когда вернётся вдохновение."
  },
  "In progress": {
    "uz": "Jarayonda",
    "ru": "В процессе"
  },
  "A place to begin": {
    "uz": "Boshlash uchun joy",
    "ru": "Место для начала"
  },
  "The next idea is yours.": {
    "uz": "Keyingi g‘oya sizniki.",
    "ru": "Следующая идея — ваша."
  },
  "Open a saved design or begin a new direction.": {
    "uz": "Saqlangan dizaynni oching yoki yangi yo‘nalishni boshlang.",
    "ru": "Откройте сохранённый дизайн или начните новое направление."
  },
  "Untitled draft": {
    "uz": "Nomsiz qoralama",
    "ru": "Черновик без названия"
  },
  "The Studio manifest must be smaller than 1 MiB.": {
    "uz": "Studio manifesti 1 MiB dan kichik bo‘lishi kerak.",
    "ru": "Манифест Studio должен быть меньше 1 МиБ."
  },
  "Choose a valid Studio JSON manifest.": {
    "uz": "Yaroqli Studio JSON manifestini tanlang.",
    "ru": "Выберите корректный JSON-манифест Studio."
  },
  "The image does not match the manifest size and type. Choose its original exported image.": {
    "uz": "Rasm hajmi va turi manifestga mos emas. Asl eksport rasmini tanlang.",
    "ru": "Размер и тип изображения не совпадают с манифестом. Выберите исходный экспортированный файл."
  },
  "The image fingerprint does not match the Studio manifest.": {
    "uz": "Rasm xeshi Studio manifestiga mos emas.",
    "ru": "Хеш изображения не совпадает с манифестом Studio."
  },
  "The image is in Media, but its Studio record was not saved.": {
    "uz": "Tasvir Media kutubxonasida, lekin uning Studio yozuvi saqlanmadi.",
    "ru": "Изображение находится в Медиатеке, но запись Studio не сохранена."
  },
  "Unsaved": {
    "uz": "Saqlanmagan",
    "ru": "Не сохранён"
  },
  "Content preparation": {
    "uz": "Kontent tayyorlash",
    "ru": "Подготовка контента"
  },
  "Prepare your content": {
    "uz": "Kontentingizni tayyorlang",
    "ru": "Подготовьте контент"
  },
  "A new draft": {
    "uz": "Yangi qoralama",
    "ru": "Новый черновик"
  },
  "Saved revision {revision}": {
    "uz": "Saqlangan versiya {revision}",
    "ru": "Сохранённая версия {revision}"
  },
  "Not saved yet": {
    "uz": "Hali saqlanmagan",
    "ru": "Ещё не сохранено"
  },
  "Choose a PNG or JPEG image.": {
    "uz": "PNG yoki JPEG rasmini tanlang.",
    "ru": "Выберите изображение PNG или JPEG."
  },
  "Use one PNG or JPEG image up to 5 MiB.": {
    "uz": "5 MiB gacha bo‘lgan bitta PNG yoki JPEG rasmidan foydalaning.",
    "ru": "Используйте одно изображение PNG или JPEG до 5 МиБ."
  },
  "Checking…": {
    "uz": "Tekshirilmoqda…",
    "ru": "Проверка…"
  },
  "Heartbeat received": {
    "uz": "Faollik signali olindi",
    "ru": "Сигнал активности получен"
  },
  "Heartbeat overdue": {
    "uz": "Faollik signali kechikdi",
    "ru": "Сигнал активности просрочен"
  },
  "Waiting for first heartbeat": {
    "uz": "Birinchi faollik signali kutilmoqda",
    "ru": "Ожидание первого сигнала активности"
  },
  "The foundation API could not be reached. Start the service, then try again.": {
    "uz": "Asosiy APIga ulanib bo‘lmadi. Xizmatni ishga tushirib, qayta urinib ko‘ring.",
    "ru": "Основной API недоступен. Запустите службу и повторите."
  },
  "The status check timed out. Try again when the service is responding.": {
    "uz": "Holat tekshiruvi vaqti tugadi. Xizmat javob berganda qayta urinib ko‘ring.",
    "ru": "Время проверки истекло. Повторите, когда служба будет отвечать."
  },
  "The service returned an unexpected status response. Check the API version.": {
    "uz": "Xizmat kutilmagan holat javobini qaytardi. API versiyasini tekshiring.",
    "ru": "Служба вернула неожиданный ответ состояния. Проверьте версию API."
  },
  "The service could not return its status. Check its logs, then try again.": {
    "uz": "Xizmat holatni qaytara olmadi. Jurnallarni tekshirib, qayta urinib ko‘ring.",
    "ru": "Служба не вернула состояние. Проверьте журналы и повторите."
  },
  "The status check could not be completed. Try again.": {
    "uz": "Holat tekshiruvi yakunlanmadi. Qayta urinib ko‘ring.",
    "ru": "Проверка состояния не завершена. Повторите попытку."
  },
  "draft": {
    "uz": "Qoralama",
    "ru": "Черновик"
  },
  "active": {
    "uz": "Faol",
    "ru": "Активный"
  },
  "archived": {
    "uz": "Arxivlangan",
    "ru": "Архивный"
  },
  "approved": {
    "uz": "Tasdiqlangan",
    "ru": "Одобрено"
  },
  "in-review": {
    "uz": "Tekshiruvda",
    "ru": "На проверке"
  },
  "Total unavailable": {
    "uz": "Jami son mavjud emas",
    "ru": "Общее число недоступно"
  },
  "Unreviewed Studio visualization": {
    "uz": "Hali ko‘rib chiqilmagan Studio tasviri",
    "ru": "Визуализация Studio, ожидающая проверки"
  },
  "Unassigned render": {
    "uz": "Mahsulotga biriktirilmagan tasvir",
    "ru": "Рендер без назначенного товара"
  },
  "unreviewed": {
    "uz": "Ko‘rib chiqilmagan",
    "ru": "Ожидает проверки"
  },
  "New color": {
    "uz": "Yangi rang",
    "ru": "Новый цвет"
  },
  "Import Studio image": {
    "uz": "Studio tasvirini import qilish",
    "ru": "Импортировать изображение Studio"
  },
  "About this app: {version}": {
    "uz": "Ilova haqida: {version}",
    "ru": "О приложении: {version}"
  },
  "Not checked": {
    "uz": "Tekshirilmagan",
    "ru": "Не проверено"
  },
  "Unavailable": {
    "uz": "Mavjud emas",
    "ru": "Недоступно"
  }
};

export const uiMessages: UiMessageTable = { ...baseUiMessages, ...integrationUiMessages, ...operatorUiMessages, ...designerUiMessages, ...productReviewUiMessages, ...catalogueUiMessages, ...sourcingUiMessages, ...sourcingViewUiMessages, ...eventsUiMessages };

/** Resolves exact interface phrases without interpreting or translating customer-authored data. */
export function translateUi(source: string, locale: UiLocale, variables?: Record<string, string | number>): string {
  const message = locale === 'en' ? source : uiMessages[source]?.[locale] ?? source;
  return message.replace(/\{(\w+)\}/g, (match, key: string) => variables && Object.hasOwn(variables, key) ? String(variables[key]) : match);
}
