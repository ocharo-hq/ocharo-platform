import type { UiMessageTable } from './UiMessages';

/** Product helpers and retained catalogue operator controls; authored content stays separate. */
export const catalogueLegacyUiMessages: UiMessageTable = {
  '{count} open questions': {
    uz: '{count} ta ochiq savol',
    ru: 'Открытых вопросов: {count}',
  },
  'Removed images leave the product list when saved; their files remain stored.': {
    uz: 'Olib tashlangan rasmlar saqlaganda mahsulot ro‘yxatidan chiqadi; fayllar saqlanib qoladi.',
    ru: 'Удалённые изображения исчезнут из списка товара при сохранении; файлы останутся в хранилище.',
  },
  'Save or cancel your changes first.': {
    uz: 'Avval o‘zgarishlarni saqlang yoki bekor qiling.',
    ru: 'Сначала сохраните или отмените изменения.',
  },
  'Restore this revision': {
    uz: 'Shu versiyani tiklash',
    ru: 'Восстановить эту версию',
  },
  'Change note': {
    uz: 'O‘zgarish izohi',
    ru: 'Примечание об изменении',
  },
  'An active product needs an Uzbek name.': {
    uz: 'Faol mahsulotga o‘zbekcha nom kerak.',
    ru: 'Для активного товара нужно название на узбекском.',
  },
  'An active product needs at least one active image.': {
    uz: 'Faol mahsulotga kamida bitta faol rasm kerak.',
    ru: 'Для активного товара нужно хотя бы одно активное изображение.',
  },
  'Minimum quantity must be a whole number of at least 1.': {
    uz: 'Minimal miqdor 1 yoki undan katta butun son bo‘lishi kerak.',
    ru: 'Минимальное количество должно быть целым числом не меньше 1.',
  },
  'Maximum quantity cannot be less than minimum quantity.': {
    uz: 'Maksimal miqdor minimal miqdordan kichik bo‘lishi mumkin emas.',
    ru: 'Максимальное количество не может быть меньше минимального.',
  },
  'Enter a valid price.': {
    uz: 'To‘g‘ri narx kiriting.',
    ru: 'Введите корректную цену.',
  },
  'Price ranges overlap; only the last range can be open-ended.': {
    uz: 'Narx oraliqlari kesishmoqda; faqat oxirgi oraliqning yuqori chegarasi bo‘lmasligi mumkin.',
    ru: 'Диапазоны цен пересекаются; только последний диапазон может не иметь верхней границы.',
  },
  'Approved {language} copy needs a name.': {
    uz: 'Tasdiqlangan {language} matniga nom kerak.',
    ru: 'Для подтверждённого текста на языке {language} нужно название.',
  },
  'Save the product before adding images.': {
    uz: 'Rasm qo‘shishdan oldin mahsulotni saqlang.',
    ru: 'Сохраните товар перед добавлением изображений.',
  },
  'Save your changes before adding images.': {
    uz: 'Rasm qo‘shishdan oldin o‘zgarishlarni saqlang.',
    ru: 'Сохраните изменения перед добавлением изображений.',
  },
  'Restored revision {revision} as new revision {newRevision}.': {
    uz: '{revision}-versiya yangi {newRevision}-versiya sifatida tiklandi.',
    ru: 'Версия {revision} восстановлена как новая версия {newRevision}.',
  },
  'Could not connect to the catalogue service. Check that it is running.': {
    uz: 'Katalog xizmatiga ulanib bo‘lmadi. Xizmat ishlayotganini tekshiring.',
    ru: 'Не удалось подключиться к службе каталога. Проверьте, что она запущена.',
  },
  'The catalogue service returned error {status}.': {
    uz: 'Katalog xizmati {status} xatosini qaytardi.',
    ru: 'Служба каталога вернула ошибку {status}.',
  },
  'Saved {saved}; {failed} failed. Check the rows.': {
    uz: '{saved} ta saqlandi; {failed} tasida xato. Qatorlarni tekshiring.',
    ru: 'Сохранено: {saved}; ошибок: {failed}. Проверьте строки.',
  },
  'Saved copy for {count} products.': {
    uz: '{count} ta mahsulot matni saqlandi.',
    ru: 'Тексты сохранены для {count} товаров.',
  },
  'Price saved for {product}. Confirm the draft price before publication.': {
    uz: '{product} narxi saqlandi. Nashrdan oldin qoralama narxni tasdiqlang.',
    ru: 'Цена для {product} сохранена. Подтвердите черновую цену перед публикацией.',
  },
  'Settings saved.': {
    uz: 'Sozlamalar saqlandi.',
    ru: 'Настройки сохранены.',
  },
  'Import: {created} created, {updated} updated, {unchanged} unchanged.': {
    uz: 'Import: {created} ta yangi, {updated} ta yangilandi, {unchanged} ta o‘zgarishsiz.',
    ru: 'Импорт: создано {created}, обновлено {updated}, без изменений {unchanged}.',
  },
  '{action}: updated {count} products.': {
    uz: '{action}: {count} ta mahsulot yangilandi.',
    ru: '{action}: обновлено товаров — {count}.',
  },
  'Skipped: {items}': {
    uz: 'O‘tkazib yuborildi: {items}',
    ru: 'Пропущено: {items}',
  },
  'Created {product} as a draft.': {
    uz: '{product} qoralama sifatida yaratildi.',
    ru: '{product} создан как черновик.',
  },
  'Product copy': {
    uz: 'Mahsulot matnlari',
    ru: 'Тексты товаров',
  },
  'Fill and approve copy in three languages.': {
    uz: 'Uch tildagi matnni to‘ldiring va tasdiqlang.',
    ru: 'Заполните и подтвердите тексты на трёх языках.',
  },
  '{approved} / {total} language versions approved.': {
    uz: '{approved} / {total} tildagi matn tasdiqlangan.',
    ru: 'Подтверждено языковых версий: {approved} / {total}.',
  },
  'A name is required before approval.': {
    uz: 'Tasdiqlashdan oldin nom kiriting.',
    ru: 'Перед подтверждением введите название.',
  },
  'Save {count} changes': {
    uz: '{count} ta o‘zgarishni saqlash',
    ru: 'Сохранить изменений: {count}',
  },
  'Only missing or unapproved': {
    uz: 'Faqat bo‘sh yoki tasdiqlanmaganlar',
    ru: 'Только незаполненные или неподтверждённые',
  },
  Uzbek: {
    uz: 'O‘zbekcha',
    ru: 'Узбекский',
  },
  Russian: {
    uz: 'Ruscha',
    ru: 'Русский',
  },
  English: {
    uz: 'Inglizcha',
    ru: 'Английский',
  },
  Prices: {
    uz: 'Narxlar',
    ru: 'Цены',
  },
  'Edit a cell and press Enter or click elsewhere to save a draft price.': {
    uz: 'Katakni tahrirlab, Enter yoki boshqa joyni bosing — narx qoralama sifatida saqlanadi.',
    ru: 'Измените ячейку и нажмите Enter или щёлкните вне её, чтобы сохранить черновую цену.',
  },
  'Select products on the Products page to apply a percentage adjustment.': {
    uz: 'Foizli o‘zgarish qo‘llash uchun Mahsulotlar sahifasida mahsulotlarni tanlang.',
    ru: 'Для изменения цен на процент выберите товары на странице «Товары».',
  },
  Type: {
    uz: 'Turi',
    ru: 'Тип',
  },
  '1 piece': {
    uz: '1 dona',
    ru: '1 штука',
  },
  'Wholesale (10+)': {
    uz: 'Ulgurji (10+)',
    ru: 'Оптом (10+)',
  },
  'Price status': {
    uz: 'Narx holati',
    ru: 'Статус цены',
  },
  'Retail price for {product}': {
    uz: '{product} uchun bir dona narxi',
    ru: 'Розничная цена для {product}',
  },
  'Wholesale price for {product}': {
    uz: '{product} uchun ulgurji narx',
    ru: 'Оптовая цена для {product}',
  },
  'No price': {
    uz: 'Narx yo‘q',
    ru: 'Нет цены',
  },
  'Page not found.': {
    uz: 'Sahifa topilmadi.',
    ru: 'Страница не найдена.',
  },
  'Back to products': {
    uz: 'Mahsulotlarga qaytish',
    ru: 'Вернуться к товарам',
  },
  'Catalogue sheet': {
    uz: 'Katalog varag‘i',
    ru: 'Лист каталога',
  },
  'Only active products are included. Choose “Save as PDF” in the print dialog.': {
    uz: 'Faqat faol mahsulotlar kiritiladi. Chop etish oynasida “PDF sifatida saqlash”ni tanlang.',
    ru: 'Включены только активные товары. В окне печати выберите «Сохранить как PDF».',
  },
  'All products': {
    uz: 'Barcha mahsulotlar',
    ru: 'Все товары',
  },
  Lookbook: {
    uz: 'Lookbook',
    ru: 'Лукбук',
  },
  'Order sheet': {
    uz: 'Buyurtma varag‘i',
    ru: 'Бланк заказа',
  },
  Print: {
    uz: 'Chop etish',
    ru: 'Печать',
  },
  'Loading product copy': {
    uz: 'Mahsulot matnlari yuklanmoqda',
    ru: 'Загрузка текстов товаров',
  },
};
