import { Pipe, PipeTransform } from '@angular/core';

/** "3 hours ago", relative to now. Pair it with a `title` carrying the absolute time. */
@Pipe({ name: 'timeAgo' })
export class TimeAgoPipe implements PipeTransform {
  transform(value: string | Date | null | undefined, now: Date = new Date()): string {
    if (!value) return '';

    const then = typeof value === 'string' ? new Date(value) : value;
    const seconds = Math.round((now.getTime() - then.getTime()) / 1000);

    if (seconds < 45) return 'just now';
    if (seconds < 90) return 'a minute ago';

    const minutes = Math.round(seconds / 60);
    if (minutes < 60) return `${minutes} minutes ago`;

    const hours = Math.round(minutes / 60);
    if (hours < 2) return 'an hour ago';
    if (hours < 24) return `${hours} hours ago`;

    const days = Math.round(hours / 24);
    if (days < 2) return 'yesterday';
    if (days < 30) return `${days} days ago`;

    const months = Math.round(days / 30);
    if (months < 2) return 'a month ago';
    if (months < 12) return `${months} months ago`;

    const years = Math.round(days / 365);
    return years < 2 ? 'a year ago' : `${years} years ago`;
  }
}
