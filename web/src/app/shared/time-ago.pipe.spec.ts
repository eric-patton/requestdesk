import { TimeAgoPipe } from './time-ago.pipe';

describe('TimeAgoPipe', () => {
  const pipe = new TimeAgoPipe();
  const now = new Date('2026-09-06T12:00:00Z');

  const cases: [string, string][] = [
    ['2026-09-06T11:59:50Z', 'just now'],
    ['2026-09-06T11:58:45Z', 'a minute ago'],
    ['2026-09-06T11:40:00Z', '20 minutes ago'],
    ['2026-09-06T10:45:00Z', 'an hour ago'],
    ['2026-09-06T06:00:00Z', '6 hours ago'],
    ['2026-09-05T12:00:00Z', 'yesterday'],
    ['2026-09-01T12:00:00Z', '5 days ago'],
    ['2026-08-01T12:00:00Z', 'a month ago'],
    ['2026-03-06T12:00:00Z', '6 months ago'],
    ['2025-09-06T12:00:00Z', 'a year ago'],
    ['2023-09-06T12:00:00Z', '3 years ago'],
  ];

  for (const [input, expected] of cases) {
    it(`renders ${input} as "${expected}"`, () => {
      expect(pipe.transform(input, now)).toBe(expected);
    });
  }

  it('renders nothing for a missing value', () => {
    expect(pipe.transform(null, now)).toBe('');
    expect(pipe.transform(undefined, now)).toBe('');
  });
});
