import type { UiMessageTable } from './UiMessages';

/** Stock and collection operator controls; authored names stay outside this vocabulary. */
export const catalogueOperationsUiMessages: UiMessageTable = {
  'Loading…': { uz: 'Yuklanmoqda…', ru: 'Загрузка…' },
  'Remove product': { uz: 'Mahsulotni olib tashlash', ru: 'Убрать товар' },
  'Pieces by size: {count} in total. Only availability is public; exact quantities stay private.': {
    uz: 'O‘lcham bo‘yicha jami {count} dona mavjud. Ommaviy katalogda faqat mavjudlik holati ko‘rinadi; aniq miqdorlar ichki qoladi.',
    ru: 'Всего по размерам: {count} шт. В открытом каталоге виден только статус наличия; точные остатки остаются внутренними.',
  },
  'Save changes ({count})': { uz: 'O‘zgarishlarni saqlash ({count})', ru: 'Сохранить изменения ({count})' },
  Tracking: { uz: 'Hisob', ru: 'Учёт' },
  'Track quantities': { uz: 'Miqdorlarni hisoblash', ru: 'Учитывать количество' },
  'Quantity for {product}, size {size}': {
    uz: '{product}, {size} o‘lchamidagi miqdor',
    ru: 'Количество для {product}, размер {size}',
  },
  'Saved: {saved}. Failed: {failed}. Check the affected rows.': {
    uz: 'Saqlandi: {saved}. Xato: {failed}. Xatoli qatorlarni tekshiring.',
    ru: 'Сохранено: {saved}. Ошибок: {failed}. Проверьте соответствующие строки.',
  },
  'Stock saved for {count} products.': {
    uz: '{count} ta mahsulot qoldig‘i saqlandi.',
    ru: 'Сохранены остатки товаров: {count}.',
  },
  'Delete this collection? Its products will remain.': {
    uz: 'Bu to‘plam o‘chirilsinmi? Mahsulotlar saqlanib qoladi.',
    ru: 'Удалить эту коллекцию? Товары останутся.',
  },
  'Delete collection': { uz: 'To‘plamni o‘chirish', ru: 'Удалить коллекцию' },
  'Products: {count}': { uz: 'Mahsulotlar: {count}', ru: 'Товаров: {count}' },
  Internal: { uz: 'Ichki', ru: 'Внутренняя' },
  'Collection “{name}” saved.': { uz: '“{name}” to‘plami saqlandi.', ru: 'Коллекция «{name}» сохранена.' },
  'Collection deleted. Its products remain.': {
    uz: 'To‘plam o‘chirildi. Mahsulotlar saqlanib qoldi.',
    ru: 'Коллекция удалена. Товары сохранены.',
  },
};
