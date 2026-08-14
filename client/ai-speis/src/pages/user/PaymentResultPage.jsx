import { useEffect } from 'react';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';

function PaymentResultPage() {
  useEffect(() => {
    const searchString = window.location.search;
    navigate(`${USER_ROUTES.PACKAGES}${searchString}`, { replace: true });
  }, []);

  return null;
}

export default PaymentResultPage;
