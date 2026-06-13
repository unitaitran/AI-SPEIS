import {
  BookOpen,
  FileText,
  Grid2X2,
  History,
  Layers3,
  MessageCircle,
  Package,
} from 'lucide-react';

export const STUDENT_MENU_SECTIONS = [
  {
    label: 'Chính',
    items: [
      { id: 'dashboard', label: 'Trang chủ', path: '/dashboard', icon: Grid2X2 },
      { id: 'cv', label: 'CV của tôi', path: '/my-cv', icon: FileText },
    ],
  },
  {
    label: 'Luyện tập',
    items: [
      { id: 'questions', label: 'Câu hỏi', path: '/questions', icon: BookOpen },
      { id: 'history', label: 'Lịch sử phỏng vấn', path: '/interview-history', icon: History },
      { id: 'flashcards', label: 'Flashcards', path: '/flashcards', icon: Layers3 },
    ],
  },
  {
    label: 'Cộng đồng',
    items: [
      { id: 'community', label: 'Cộng đồng', path: '/community', icon: MessageCircle },
    ],
  },
  {
    label: 'Quản lý',
    items: [
      { id: 'subscription', label: 'Quản lý gói', path: '/subscription', icon: Package },
    ],
  },
];
