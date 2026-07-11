export function levenshteinDistance(s, t) {
  if (!s.length) return t.length;
  if (!t.length) return s.length;

  const arr = [];
  for (let i = 0; i <= t.length; i++) {
    arr[i] = [i];
    for (let j = 1; j <= s.length; j++) {
      arr[i][j] =
        i === 0
          ? j
          : Math.min(
              arr[i - 1][j] + 1,
              arr[i][j - 1] + 1,
              arr[i - 1][j - 1] + (s[j - 1] === t[i - 1] ? 0 : 1)
            );
    }
  }
  return arr[t.length][s.length];
}

export function calculateAccuracy(expected, actual) {
  // Normalize strings: lowercase, remove punctuation, ignore [Tên của bạn] placeholder
  const normalize = (str) =>
    (str || '')
      .toLowerCase()
      .replace(/\[tên của bạn\]/g, '')
      .replace(/[.,/#!$%^&*;:{}=\-_`~()\[\]]/g, '')
      .replace(/\s{2,}/g, ' ')
      .trim();

  const normExpected = normalize(expected);
  const normActual = normalize(actual);

  if (!normExpected) return 0;
  
  const distance = levenshteinDistance(normExpected, normActual);
  const maxLength = Math.max(normExpected.length, normActual.length);
  
  if (maxLength === 0) return 100;
  
  const accuracy = ((maxLength - distance) / maxLength) * 100;
  return Math.max(0, Math.round(accuracy));
}
