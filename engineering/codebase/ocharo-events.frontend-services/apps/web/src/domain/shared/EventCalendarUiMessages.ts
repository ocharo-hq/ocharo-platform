import type { UiLocale } from '@/domain/shared/UiMessages';

/** The labels shared by the event calendar action and its exported reminders. */
export const eventCalendarUiMessages = {
  en: {
    download: 'Download calendar',
    downloadDeadline: 'Download deadline',
    exactDateNeeded: 'An exact event date or application deadline is needed for calendar export.',
    downloadFailed: 'The calendar file could not be downloaded.',
    deadline: 'Application deadline',
    deadlineNote: 'Date-only reminder. Confirm the submission time and time zone with the organizer.',
    organizer: 'Organizer',
    participation: 'How to take part',
    cost: 'Cost',
    website: 'Website',
    source: 'Source',
  },
  ru: {
    download: 'Скачать календарь',
    downloadDeadline: 'Скачать срок подачи',
    exactDateNeeded: 'Для экспорта нужна точная дата события или крайний срок подачи заявки.',
    downloadFailed: 'Не удалось скачать файл календаря.',
    deadline: 'Крайний срок подачи заявки',
    deadlineNote: 'Напоминание на день. Уточните время подачи и часовой пояс у организатора.',
    organizer: 'Организатор',
    participation: 'Как принять участие',
    cost: 'Стоимость',
    website: 'Сайт',
    source: 'Источник',
  },
  uz: {
    download: 'Taqvimni yuklab olish',
    downloadDeadline: 'Ariza muddatini yuklab olish',
    exactDateNeeded: 'Taqvimga eksport qilish uchun tadbir sanasi yoki ariza muddati aniq bo‘lishi kerak.',
    downloadFailed: 'Taqvim faylini yuklab bo‘lmadi.',
    deadline: 'Ariza topshirishning oxirgi kuni',
    deadlineNote: 'Kunlik eslatma. Topshirish vaqti va vaqt mintaqasini tashkilotchidan aniqlashtiring.',
    organizer: 'Tashkilotchi',
    participation: 'Ishtirok etish tartibi',
    cost: 'Xarajat',
    website: 'Veb-sayt',
    source: 'Manba',
  },
} satisfies Record<UiLocale, Record<string, string>>;
